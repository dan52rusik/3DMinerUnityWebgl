using System;
using System.Collections.Generic;
using SimpleVoxelSystem.Data;
using UnityEngine;

namespace SimpleVoxelSystem
{
    internal static class BlockTypeCatalog
    {
        private static readonly BlockType[] EditableTypes = BuildEditableTypes();
        private static readonly int MaxTypeId = GetMaxTypeId();

        public static IReadOnlyList<BlockType> GetEditableTypes() => EditableTypes;

        public static int ClampStoredTypeId(int rawTypeId)
        {
            return Mathf.Clamp(rawTypeId, (int)BlockType.Dirt, MaxTypeId);
        }

        public static string GetEditorLabel(BlockType type)
        {
            return $"[{(int)type}] {type}";
        }

        public static Color GetColor(BlockType type)
        {
            switch (type)
            {
                case BlockType.Dirt:
                    return new Color(0.55f, 0.27f, 0.07f);
                case BlockType.Stone:
                    return new Color(0.50f, 0.50f, 0.50f);
                case BlockType.Iron:
                    return new Color(0.65f, 0.44f, 0.40f);
                case BlockType.Gold:
                    return new Color(1.00f, 0.84f, 0.00f);
                case BlockType.Grass:
                    return new Color(0.20f, 0.80f, 0.20f);
                default:
                    return Color.magenta;
            }
        }

        private static BlockType[] BuildEditableTypes()
        {
            Array values = Enum.GetValues(typeof(BlockType));
            List<BlockType> editable = new List<BlockType>(values.Length);

            foreach (BlockType type in values)
            {
                if (type == BlockType.Air)
                    continue;

                editable.Add(type);
            }

            return editable.ToArray();
        }

        private static int GetMaxTypeId()
        {
            int max = 0;
            foreach (BlockType type in Enum.GetValues(typeof(BlockType)))
                max = Mathf.Max(max, (int)type);
            return max;
        }
    }
}
