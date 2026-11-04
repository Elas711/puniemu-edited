# In-game web pages

The game shows notice pages in a web view. Puniemu serves them from this folder:

| URL | File |
| --- | --- |
| `/web/noticeBannerPage.nhn` | `Page/NoticeBannerPage/noticeBannerPage.html` |
| `/web/viewpage.nhn?webviewId=ID&pageNo=N` | `Page/ID/viewpage.html` (page 1) or `Page/ID/viewpageN.html` |
| `/web/static/...` | any file in this folder (images, css, js) |
| `/web/portal.nhn` | built-in player portal (event selection) |

When a file is missing, a built-in page with the server news (admin panel → Settings) and the current
events is shown instead.

These placeholders are replaced inside the HTML files: `{{SERVER_NAME}}`, `{{MOTD}}`, `{{PORTAL_URL}}`.
