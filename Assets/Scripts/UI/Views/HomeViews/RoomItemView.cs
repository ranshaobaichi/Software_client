using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using UI.Models;
using UI.StateEngine;
using UI.ViewModels;

namespace UI.Views {
    public class RoomItemView : MonoBehaviour{
        [SerializeField]
        private Text _roomIdText;
        [SerializeField]
        private Text _roomHeadCountText;
        [SerializeField]
        private List<Image> _roomPlayerAvatarImages;
        
        private RoomItemViewModel m_viewModel;
        private UIStateFinder m_stateFinder;
        private Action<RoomModel> m_onClicked;
        
        public void Init(RoomItemViewModel viewModel, Action<RoomModel> onClicked) {
            m_viewModel = viewModel;
            m_onClicked = onClicked;

            _roomIdText.text = m_viewModel.roomId;
            _roomHeadCountText.text =
                    $"{m_viewModel.currentPlayersCount}/{m_viewModel.maxPlayersCount}";
            for (var i = 0; i < m_viewModel.currentPlayersCount; i++) {
                // TODO: Set player avatar images based on m_viewModel.playerAvatarImages
                _roomPlayerAvatarImages[i].gameObject.SetActive(i < m_viewModel.currentPlayersCount);
            }
        }

        public void OnItemClicked() => m_onClicked(m_viewModel.GetRoomModel());
    }
}