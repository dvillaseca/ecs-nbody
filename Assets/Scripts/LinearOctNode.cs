using Unity.Mathematics;

namespace nbody
{
	public struct LinearOctNode
	{
		public enum NodeType : byte
		{
			None = 0,
			External = 1,
			Internal = 2
		}

		public float3 avgPos;
		public float avgMass;

		public float3 center;
		public float size;
		public float sSize;

		public NodeType type;
		public int childsStartIndex;

		public LinearOctNode(float3 center, float size)
		{
			this.center = center;
			this.size = size;
			sSize = size * size;
			type = NodeType.None;
			avgPos = float3.zero;
			childsStartIndex = 0;
			avgMass = 0f;
		}
	}
}
