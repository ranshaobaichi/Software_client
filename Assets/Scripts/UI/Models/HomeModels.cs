using System.Collections.Generic;

namespace UI.Models {
    public class RoomModel {
        public readonly int roomId;
        public readonly int maxPlayersCount;
        public readonly List<PlayerData.PlayerBasicInfo> playerBasicInfos;

        public RoomModel(int roomId, int maxPlayersCount, List<PlayerData.PlayerBasicInfo> playerBasicInfos) {
            this.playerBasicInfos = playerBasicInfos;
            this.roomId = roomId;
            this.maxPlayersCount = maxPlayersCount;
            this.playerBasicInfos = playerBasicInfos ?? new List<PlayerData.PlayerBasicInfo>();
        }

        public int currentPlayersCount => playerBasicInfos.Count;
    }
}