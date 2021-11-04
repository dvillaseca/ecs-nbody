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
		[ReadOnly]
		public int batches;
		[ReadOnly]
		public int treeSize;

		public void Execute(ArchetypeChunk batchInChunk, int batchIndex, int index)
		{
			var transforms = batchInChunk.GetNativeArray(transformHandle);
			var bodies = batchInChunk.GetNativeArray(bodyHandle);
			var length = transforms.Length;
			for (int i = 0; i < length; i++)
			{
				var body = bodies[i];
				var oldVelocity = body.velocity;
				for (int j = 0; j < batches; j++)
				{
					Interact(j * treeSize, ref body);
				}
				var addedVelocity = body.velocity - oldVelocity;
				body.position += (oldVelocity + addedVelocity * 0.5f) * deltaTime;
				bodies[i] = body;

				var transform = transforms[i];
				transform.Value = float4x4.TRS(body.position, quaternion.identity, body.size);
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
			float sqrDist = math.lengthsq(force);
			if (node.type == LinearOctNode.NodeType.Internal && node.sSize / sqrDist > Const.COMPLEXITY)
			{
				for (int i = node.childsStartIndex; i < node.childsStartIndex + 8; i++)
				{
					Interact(i, ref body);
				}
				return;
			}
			float dist = math.sqrt(sqrDist);

			if (sqrDist < 0.002f)
				sqrDist = 0.002f;
			//if (node.type == LinearOctNode.NodeType.External)
			//{
			//	float collisionLimit = (node.bodySize + body.size) * 0.5f;
			//	if (dist < collisionLimit)
			//	{
			//		if (body.mass > node.avgMass)
			//		{
			//			float sum = 1f / (node.avgMass + body.mass);
			//			body.velocity = node.bodyVelocity * (node.avgMass * sum) + body.velocity * (body.mass * sum);
			//			body.mass += node.avgMass;
			//			body.size = Utils.MassToSize(body.mass);
			//		}
			//		else
			//		{
			//			body.mass = 0f;
			//		}
			//		return;
			//	}
			//}
			//if node is internal the distance from the avg mass could be really tiny and add an insane speed
			float strength = deltaForce * node.avgMass / (sqrDist * dist);
			body.velocity += force * strength;
		}
	}
}