using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VoidChest
{
    /// <summary>
    /// 虚空宝箱界面的自定义按钮注入（克隆原版"全部堆叠"按钮）。
    /// 单按钮：未解锁远程时为"附近存储"，解锁后为"远程存入"。
    /// </summary>
    internal static class VoidChestUi
    {
        private static GameObject _storeButton;

        internal static void OnContainerShown(Container container)
        {
            EnsureButton();

            if (_storeButton == null)
            {
                return;
            }

            if (!(container is VirtualContainer))
            {
                SetButtonActive(false);
                return;
            }

            var player = Player.m_localPlayer;
            bool remoteAvailable = VoidChestManager.CanUseRemote(player);
            bool nearbyEnabled = VoidChestPlugin.EnableNearbyStore != null && VoidChestPlugin.EnableNearbyStore.Value;

            if (!remoteAvailable && !nearbyEnabled)
            {
                SetButtonActive(false);
                return;
            }

            var label = _storeButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = remoteAvailable ? "远程存入" : "附近存储";
            }

            SetButtonActive(true);

            VLog.Debug($"界面按钮: 文本={(remoteAvailable ? "远程存入" : "附近存储")}, container={container.GetType().Name}");
        }

        private static void EnsureButton()
        {
            if (_storeButton != null)
            {
                return;
            }

            var gui = InventoryGui.instance;
            if (gui == null || gui.m_stackAllButton == null)
            {
                return;
            }

            var parent = gui.m_stackAllButton.transform.parent;
            var clone = Object.Instantiate(gui.m_stackAllButton.gameObject, parent);
            clone.name = "VoidChest_StoreButton";

            var button = clone.GetComponent<Button>();
            if (button != null)
            {
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(OnStoreClicked);
            }

            var text = clone.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = "附近存储";
            }

            clone.SetActive(false);
            _storeButton = clone;
            VLog.Info("已注入虚空宝箱存储按钮。");
        }

        private static void SetButtonActive(bool show)
        {
            if (_storeButton != null && _storeButton.activeSelf != show)
            {
                _storeButton.SetActive(show);
            }
        }

        private static void OnStoreClicked()
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }

            if (VoidChestManager.CanUseRemote(player))
            {
                VLog.Info("点击了\"远程存入\"按钮。");
                VoidChestRemoteStore.Start(player);
            }
            else
            {
                VLog.Info("点击了\"附近存储\"按钮。");
                VoidChestNearbyStore.Start(player);
            }
        }
    }
}
