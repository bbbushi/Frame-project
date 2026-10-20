namespace Project.ItemSystem
{
    public enum ItemType { Consumable, Material }

    [CreateAssetMenu(fileName = "New item", menuName = "Project/ItemData")]
    public class ItemData : ScriptableObject
    {
        public string itemId;              // 仅供三期存档序列化；运行时格子存 SO 引用，不用它做查找
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;
        public ItemType type = ItemType.Consumable;
        public int maxStack = 99;
        public GameplayEffect useEffect;   // Consumable 使用时挂给玩家（Attribute/Core 现成类型，直接引用）
    }
}