using Puniemu.Src.Server.GameServer.DataClasses;

namespace Puniemu.Src.Server.GameServer.Requests.GameUseItem.DataClasses
{
    // Sent when food is used on a Yo-kai whose amiability is already maxed. Created by WibWob_YT.
    public class GameUseItemMaxLevelResponse : GameUseItemResponse
    {
        public GameUseItemMaxLevelResponse(YwpUserData userData, string itemData, long? item_id)
            : base(userData, itemData, item_id)
        {
            this.ResultType = 2;
            this.NextScreenType = 1;
            this.DialogMsg = "This Yo-kai's Amiability is\nalready at its maximum level.";
            this.DialogTitle = "Error";
        }
    }
}
