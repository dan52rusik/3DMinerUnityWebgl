using System;
using UnityEngine;

namespace SimpleVoxelSystem.Data
{
    public enum BlockType { Air = 0, Dirt = 1, Stone = 2, Iron = 3, Gold = 4, Grass = 5 }

    [Serializable]
    public class BlockData 
    {
        public BlockType type;
        public int maxHealth; // Сколько ударов нужно
        public int reward;    // Сколько монет/ресурсов дает
        public Color blockColor; // Цвет или ссылка на материал
    }
}
