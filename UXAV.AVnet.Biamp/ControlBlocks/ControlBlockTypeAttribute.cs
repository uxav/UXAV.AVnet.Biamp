using System;

namespace UXAV.AVnet.Biamp.ControlBlocks
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ControlBlockTypeAttribute : Attribute
    {
        public ControlBlockTypeAttribute(TesiraBlockType type)
        {
            Type = type;
        }

        public TesiraBlockType Type { get; }
    }
}