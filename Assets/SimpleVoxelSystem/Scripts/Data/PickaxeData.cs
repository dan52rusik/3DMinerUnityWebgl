using UnityEngine;

namespace SimpleVoxelSystem.Data
{
    [CreateAssetMenu(fileName = "NewPickaxe", menuName = "SimpleVoxelSystem/Pickaxe Data")]
    public class PickaxeData : ScriptableObject
    {
        public string displayName;
        public string description;
        public int buyPrice;
        public int miningPower;
        public int requiredMiningLevel;
        public Color iconColor = Color.white;
    }
}
