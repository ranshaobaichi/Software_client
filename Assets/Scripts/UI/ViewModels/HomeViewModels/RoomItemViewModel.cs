using System.Collections.Generic;
using Constants;
using UI.Models;

namespace UI.ViewModels {
    public class RoomItemViewModel {
        public string roomId;
        public int currentPlayersCount;
        public int maxPlayersCount;
        public List<PlayerAvatarColor> playerAvatarImages;
        
        private RoomModel m_roomModel;
        
        public RoomItemViewModel(RoomModel info) {
            m_roomModel = info;
            
            // TODO: Set ViewModel data based on RoomModel
        }
        
        public RoomModel GetRoomModel() => m_roomModel;
    }
}