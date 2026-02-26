using System;
using UnityEngine;

namespace SimpleVoxelSystem.Data
{
    public enum BlockType { Dirt, Stone, Iron, Gold }

    [Serializable]
    public class BlockData 
    {
        public BlockType type;
        public int maxHealth; // Сколько ударов нужно
        public int reward;    // Сколько монет/ресурсов дает
        public Color blockColor; // Цвет или ссылка на материал
    }
}
