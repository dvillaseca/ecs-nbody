using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;

namespace nbody
{
	[BurstCompile]
	public struct AttractAndMove : IJobChunk
	{
		public ComponentTypeHandle<LocalToWorld> transformHandle;
		public ComponentTypeHandle<Body> bodyHandle;
		[ReadOnly]
		[DeallocateOnJobCompletion]
		public NativeArray<LinearOctNode> nodes;
		[ReadOnly]
		public float deltaTime;
		[ReadOnly]
		public float deltaForce;

		public void Execute(ArchetypeChunk batchInChunk, int batchIndex, int index)
		{
			var transforms = batchInChunk.GetNativeArray(transformHandle);
			var bodies = batchInChunk.GetNativeArray(bodyHandle);
			var length = transforms.Length;
			for (int i = 0; i < length; i++)
			{
				var body = bodies[i];

				Interact(0, ref body);
				body.position += body.velocity * deltaTime;
				bodies[i] = body;

				var transform = transforms[i];
				var size = Utils.MassToSize(body.mass);
				transform.Value = float4x4.TRS(body.position, quaternion.identity, new float3(size));
				transforms[i] = transform;
			}
		}
		private void Interact(int index, ref Body body)
		{
			var node = nodes[index];
			if (node.type == LinearOctNode.NodeType.None)
				return;
			var force = node.avgPos - body.position;
			if (math.abs(force.x) < Const.EPSILON && math.abs(force.y) < Const.EPSILON && math.abs(force.z) < Const.EPSILON)
				return;
			float mag = math.lengthsq(force);
			if (node.type == LinearOctNode.NodeType.Internal && node.sSize / mag > Const.COMPLEXITY)
			{
				for (int i = node.childsStartIndex; i < node.childsStartIndex + 8; i++)
				{
					Interact(i, ref body);
				}
				return;
			}
			float dist = mag;
			if (dist < 0.002f)
				dist = 0.002f;
			//if node is internal the distance from the avg mass could be really tiny and add an insane speed
			float strength = deltaForce * node.avgMass / (dist * math.sqrt(mag));
			body.velocity += force * strength;
		}
	}
}