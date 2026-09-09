using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Items/ItemDatabase")]
public class ItemDatabase : ScriptableObject {
    public List<ItemSO> ItemList;
}
