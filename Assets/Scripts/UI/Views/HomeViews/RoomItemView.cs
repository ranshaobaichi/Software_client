using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using Constants;
using UI.Models;
using UI.ViewModels;

namespace UI.Views {
    public class RoomItemView : ViewBase<RoomItemViewModel> {
        [SerializeField]
        private Text _roomIdText;

        [SerializeField]
        private Text _roomHeadCountText;

        [SerializeField]
        private List<Image> _roomPlayerAvatarImages;

        private Action<RoomModel> m_onClicked;

   
        public void Init(RoomItemViewModel viewModel, Action<RoomModel> onClicked) {
            m_onClicked = onClicked;
            SetViewModel(viewModel); 
        }

        protected override void Render() {
            if (ViewModel == null) return;

            _roomIdText.text = ViewModel.RoomId;
            _roomHeadCountText.text =
                    $"{ViewModel.CurrentPlayersCount}/{ViewModel.MaxPlayersCount}";

            var avatars = ViewModel.PlayerAvatarColorInfos;
            int count = avatars?.Count ?? 0;

            for (int i = 0; i < _roomPlayerAvatarImages.Count; i++) {
                var img = _roomPlayerAvatarImages[i];

                if (i < count) {
                    var data = avatars[i];

                    img.gameObject.SetActive(true);
                    img.color = AvatarColorConfig.GetColor(data.color);
                }
                else {
                    img.gameObject.SetActive(false);
                }
            }
        }

        public void OnItemClicked() =>
                m_onClicked?.Invoke(ViewModel.GetRoomModel());
    }
}