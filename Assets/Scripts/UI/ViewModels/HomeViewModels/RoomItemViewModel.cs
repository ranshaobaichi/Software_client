using System.Collections.Generic;
using Constants;
using UI.Models;

namespace UI.ViewModels {
    public class RoomItemViewModel : ViewModelBase<RoomItemViewModel> {
        private string m_roomId;
        private int m_currentPlayersCount;
        private int m_maxPlayersCount;
        private List<PlayerAvatarColor> m_playerAvatarColorInfos;

        private RoomModel m_roomModel;

        public string RoomId {
            get => m_roomId;
            private set {
                if (m_roomId == value) return;
                m_roomId = value;
                RaisePropertyChanged();
            }
        }

        public int CurrentPlayersCount {
            get => m_currentPlayersCount;
            private set {
                if (m_currentPlayersCount == value) return;
                m_currentPlayersCount = value;
                RaisePropertyChanged();
            }
        }

        public int MaxPlayersCount {
            get => m_maxPlayersCount;
            private set {
                if (m_maxPlayersCount == value) return;
                m_maxPlayersCount = value;
                RaisePropertyChanged();
            }
        }

        public List<PlayerAvatarColor> PlayerAvatarColorInfos {
            get => m_playerAvatarColorInfos;
            private set {
                m_playerAvatarColorInfos = value;
                RaisePropertyChanged();
            }
        }

        public RoomItemViewModel(RoomModel info) {
            UpdateFromModel(info);
        }
        
        public void UpdateFromModel(RoomModel info) {
            m_roomModel = info;

            RoomId = info.roomId.ToString();
            MaxPlayersCount = info.maxPlayersCount;
            CurrentPlayersCount = info.playerBasicInfos?.Count ?? 0;

            var list = new List<PlayerAvatarColor>();

            if (info.playerBasicInfos != null) {
                foreach (var player in info.playerBasicInfos) {
                    list.Add(new PlayerAvatarColor {
                        uid = player.uid,
                        color = (AvatarColorID)player.color
                    });
                }
            }
            
            PlayerAvatarColorInfos = list;
        }

        public RoomModel GetRoomModel() => m_roomModel;
    }
}