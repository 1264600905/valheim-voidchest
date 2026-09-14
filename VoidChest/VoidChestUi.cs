using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VoidChest
{
    /// <summary>
    /// 虚空宝箱界面的自定义按钮注入（克隆原版"全部堆叠"按钮）。
    /// </summary>
    internal static class VoidChestUi
    {
        private static GameObject _nearbyButton;
        private static GameObject _remoteButton;

        internal static void OnContainerShown(Container container)
        {
            EnsureButtons();

            bool isChest = container is VirtualContainer;

            SetButtonActive(_nearbyButton, isChest &&
                VoidChestPlugin.EnableNearbyStore != null && VoidChestPlugin.EnableNearbyStore.Value);
            SetButtonActive(_remoteButton, isChest &&
                VoidChestPlugin.EnableRemoteStore != null && VoidChestPlugin.EnableRemoteStore.Value);

            VLog.Debug($"界面按钮: nearby={IsActive(_nearbyButton)}, remote={IsActive(_remoteButton)}, container={(container != null ? container.GetType().Name : "null")}");
        }

        private static void EnsureButtons()
        {
            if (_nearbyButton != null && _remoteButton != null)
            {
                return;
            }

            var gui = InventoryGui.instance;
            if (gui == null || gui.m_stackAllButton == null)
            {
                return;
            }

            if (_nearbyButton == null)
            {
                _nearbyButton = CreateButton(gui, "VoidChest_NearbyStoreButton", "附近存储", OnNearbyStoreClicked);
            }

            if (_remoteButton == null)
            {
                _remoteButton = CreateButton(gui, "VoidChest_RemoteStoreButton", "远程存入", OnRemoteStoreClicked);
            }
        }

        private static GameObject CreateButton(InventoryGui gui, string name, string text,
            UnityEngine.Events.UnityAction action)
        {
            var parent = gui.m_stackAllButton.transform.parent;
            var clone = Object.Instantiate(gui.m_stackAllButton.gameObject, parent);
            clone.name = name;

            var button = clone.GetComponent<Button>();
            if (button != null)
            {
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(action);
            }

            var label = clone.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = text;
            }

            clone.SetActive(false);
            VLog.Info($"已注入\"{text}\"按钮。");
            return clone;
        }

        private static void SetButtonActive(GameObject button, bool show)
        {
            if (button != null && button.activeSelf != show)
            {
                button.SetActive(show);
            }
        }

        private static bool IsActive(GameObject go)
        {
            return go != null && go.activeSelf;
        }

        private static void OnNearbyStoreClicked()
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }

            VLog.Info("点击了\"附近存储\"按钮。");
            VoidChestNearbyStore.Start(player);
        }

        private static void OnRemoteStoreClicked()
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }

            VLog.Info("点击了\"远程存入\"按钮。");
            VoidChestRemoteStore.Start(player);
        }
    }
}
