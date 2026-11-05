# Extracts request / response JSON keys per protocol from an unpacked x86 libSGF.so (Wibble Wobble 2.2.6).
# usage: python Tools/extract_protocols.py path/to/libSGF.so OUTDIR   -> OUTDIR/protocols.json (needs capstone, pyelftools, c++filt)
import json, re, sys, collections
import capstone
from capstone import x86 as X
from elftools.elf.elffile import ELFFile
so, S = sys.argv[1], sys.argv[2]
f = open(so, 'rb'); e = ELFFile(f); blob = open(so, 'rb').read()
secs = [(s['sh_addr'], s['sh_addr'] + s['sh_size'], s['sh_offset'], s.name) for s in e.iter_sections() if s['sh_addr']]
def off(a):
    for lo, hi, o, n in secs:
        if lo <= a < hi: return o + a - lo, n
    return None, None
def cstr(a):
    o, n = off(a)
    if o is None or n not in ('.rodata', '.data.rel.ro', '.data'): return None
    end = blob.find(b'\0', o, o + 200)
    if end <= o: return None
    s = blob[o:end]
    try: t = s.decode('utf-8')
    except UnicodeDecodeError: return None
    return t if re.fullmatch(r'[A-Za-z_][A-Za-z0-9_./]{1,60}', t) else None
import os, subprocess
if not os.path.exists(S + '/syms.json'):
    os.makedirs(S, exist_ok=True)
    tabs = [e.get_section_by_name(n) for n in ('.dynsym', '.symtab')]
    syms = [(x.name, x['st_value'], x['st_size']) for t in tabs if t for x in t.iter_symbols() if x['st_value'] and x['st_info']['type'] == 'STT_FUNC']
    dm = subprocess.run(['c++filt'], input='\n'.join(n for n, _, _ in syms), capture_output=True, text=True).stdout.split('\n')
    json.dump(syms, open(S + '/syms.json', 'w')); json.dump(dm, open(S + '/syms_dm.json', 'w'))
syms = json.load(open(S + '/syms.json')); dm = json.load(open(S + '/syms_dm.json'))
thunks = {a: n for (n, a, sz), d in zip(syms, dm) if 'get_pc_thunk' in n}
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32); md.detail = True
REG = {X.X86_REG_EAX: 'eax', X.X86_REG_EBX: 'ebx', X.X86_REG_ECX: 'ecx', X.X86_REG_EDX: 'edx', X.X86_REG_ESI: 'esi', X.X86_REG_EDI: 'edi', X.X86_REG_EBP: 'ebp'}
def strings_of(addr, size):
    o, _ = off(addr)
    if o is None: return []
    regs, out, last_thunk = {}, [], None
    stack = {}  # esp offset -> byte (libc++ short strings are built on the stack)
    prev = None
    for ins in md.disasm(blob[o:o + size], addr):
        # call next; pop ebx; add ebx, imm  ->  ebx = GOT
        if ins.mnemonic == 'call' and ins.operands and ins.operands[0].type == X.X86_OP_IMM and ins.operands[0].imm == ins.address + ins.size:
            prev = ins.address + ins.size; continue
        if ins.mnemonic == 'pop' and prev is not None and ins.operands[0].type == X.X86_OP_REG:
            regs[REG.get(ins.operands[0].reg)] = prev; prev = None; continue
        prev = None
        if ins.mnemonic == 'mov' and len(ins.operands) == 2 and ins.operands[0].type == X.X86_OP_MEM and ins.operands[1].type == X.X86_OP_IMM \
                and ins.operands[0].mem.base == X.X86_REG_ESP and ins.operands[0].mem.index == 0:
            k, v, n = ins.operands[0].mem.disp, ins.operands[1].imm & 0xFFFFFFFF, ins.operands[0].size
            for b in range(n): stack[k + b] = (v >> (8 * b)) & 0xFF
            continue
        if ins.mnemonic == 'lea' and ins.operands[1].type == X.X86_OP_MEM and ins.operands[1].mem.base == X.X86_REG_ESP:
            k = ins.operands[1].mem.disp
            ln = stack.get(k)
            if ln is not None and ln % 2 == 0 and 2 <= ln <= 44:
                bs = [stack.get(k + 1 + i) for i in range(ln // 2)]
                if all(b is not None and 32 < b < 127 for b in bs):
                    t = bytes(bs).decode()
                    if re.fullmatch(r'[A-Za-z_][A-Za-z0-9_]*', t): out.append(t)
            continue
        if ins.mnemonic == 'call' and ins.operands and ins.operands[0].type == X.X86_OP_IMM and ins.operands[0].imm in thunks:
            r = thunks[ins.operands[0].imm].rsplit('.', 1)[-1]
            regs[r] = ins.address + ins.size; continue
        if ins.mnemonic == 'add' and len(ins.operands) == 2 and ins.operands[0].type == X.X86_OP_REG and ins.operands[1].type == X.X86_OP_IMM:
            r = REG.get(ins.operands[0].reg)
            if r in regs: regs[r] = (regs[r] + ins.operands[1].imm) & 0xFFFFFFFF
            continue
        for op in ins.operands:
            if op.type == X.X86_OP_MEM and REG.get(op.mem.base) in regs and op.mem.index == 0:
                t = cstr((regs[REG[op.mem.base]] + op.mem.disp) & 0xFFFFFFFF)
                if t: out.append(t)
    return list(dict.fromkeys(out))
proto = collections.defaultdict(lambda: {'endpoint': None, 'request': [], 'response': {}, 'responses': []})
for (n, a, sz), d in zip(syms, dm):
    m = re.match(r'^Protocol([A-Z]\w+)::(domain|doParamSet)\(', d)
    if m:
        p = proto[m.group(1)]
        st = strings_of(a, sz)
        if m.group(2) == 'domain': p['endpoint'] = next((s for s in st if s.endswith('.nhn')), st[0] if st else None)
        else: p['request'] = st
        continue
    m = re.match(r'^Response([A-Z]\w+)::(response|analyze)\(', d) or re.match(r'^Response([A-Z]\w+)::(\w+)::analyze\(', d)
    if m:
        key = m.group(1) if m.group(2) in ('response', 'analyze') else m.group(1) + '.' + m.group(2)
        proto['@' + key]['request'] = list(dict.fromkeys(proto['@' + key]['request'] + strings_of(a, sz)))
    m = re.match(r'^ProtocolBase<Protocol([A-Z]\w+), (TypeList<.*)>::callSuccess', d)
    if m:
        proto[m.group(1)]['responses'] = re.findall(r'Response(\w+)', m.group(2))
out = {k: v for k, v in proto.items()}
json.dump(out, open(S + '/protocols.json', 'w'), indent=1)
print(len([k for k in out if not k.startswith('@')]), 'protocols,', len([k for k in out if k.startswith('@')]), 'response parsers')
