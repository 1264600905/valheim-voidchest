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

        internal static void OnContainerShown(Container container)
        {
            EnsureButton();

            bool show = container is VirtualContainer &&
                        VoidChestPlugin.EnableNearbyStore != null &&
                        VoidChestPlugin.EnableNearbyStore.Value;

            if (_nearbyButton != null && _nearbyButton.activeSelf != show)
            {
                _nearbyButton.SetActive(show);
            }

            VLog.Debug($"界面按钮: show={show}, container={(container != null ? container.GetType().Name : "null")}");
        }

        private static void EnsureButton()
        {
            if (_nearbyButton != null)
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
            clone.name = "VoidChest_NearbyStoreButton";

            var button = clone.GetComponent<Button>();
            if (button != null)
            {
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(OnNearbyStoreClicked);
            }

            var text = clone.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = "附近存储";
            }

            clone.SetActive(false);
            _nearbyButton = clone;
            VLog.Info("已注入\"附近存储\"按钮。");
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
    }
}
