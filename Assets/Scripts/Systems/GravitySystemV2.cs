using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections.LowLevel.Unsafe;

namespace nbody
{
	[DisableAutoCreation]
	public class GravitySystemV2 : SystemBase
	{
		private NativeArray<Body> bodies;
		private NativeArray<LinearOctNode> nodes;
		private NativeQueue<Bounds> bounds;
		private NativeArray<JobHandle> treeJobs;
		private NativeList<Body>[] bodiesList;

		protected override void OnStartRunning()
		{
			var spawnData = GetSingleton<EntitySpawnData>();
			float explosionDiameter = math.pow(spawnData.count * math.pow(Utils.MassToSize(spawnData.massRange.y), 3f), 0.3333333f);
			bodies = new NativeArray<Body>(spawnData.count, Allocator.Persistent);
			for (int i = 0; i < spawnData.count; i++)
			{
				var newEntity = EntityManager.Instantiate(spawnData.prefab);
				EntityManager.AddComponent<BodyTag>(newEntity);

				float mass = UnityEngine.Random.Range(spawnData.massRange.y, spawnData.massRange.x);
				var size = Utils.MassToSize(mass);
				var pos = UnityEngine.Random.insideUnitSphere;
				Body body = default;
				switch (spawnData.option)
				{
					case EntitySpawnData.EmitOption.explosion:
						pos *= explosionDiameter;
						body = new Body { velocity = pos * spawnData.explosionForce, mass = mass, position = pos, size = size };
						break;
					case EntitySpawnData.EmitOption.disk:

						pos.y *= spawnData.diskRadius * 0.3f;
						pos.x *= spawnData.diskRadius;
						pos.z *= spawnData.diskRadius;

						Vector3 v = Vector3.Cross(pos.normalized, Vector3.up);
						v = v.normalized * math.sqrt(Const.GRAVITY * spawnData.massRange.y / pos.magnitude) * spawnData.diskSpeed;
						body = new Body { velocity = v, mass = mass, position = pos, size = size };
						break;
				}
				bodies[i] = body;
				//EntityManager.SetComponentData(newEntity, body);
			}
			bounds = new NativeQueue<Bounds>(Allocator.Persistent);
			nodes = new NativeArray<LinearOctNode>(Const.TREE_SIZE * Const.TREE_COUNT, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
			treeJobs = new NativeArray<JobHandle>(8, Allocator.Persistent);

			bodiesList = new NativeList<Body>[8];
			for (int i = 0; i < 8; i++)
			{
				bodiesList[i] = new NativeList<Body>(bodies.Length / 2, Allocator.Persistent);
			}
		}
		protected override void OnStopRunning()
		{
			DisposeAll();
		}
		private void DisposeAll()
		{
			if (bodies.IsCreated)
				bodies.Dispose();
			if (bounds.IsCreated)
				bounds.Dispose();
			if (nodes.IsCreated)
				nodes.Dispose();
			if (treeJobs.IsCreated)
				treeJobs.Dispose();
			for (int i = 0; i < bodiesList.Length; i++)
			{
				if (bodiesList[i].IsCreated)
					bodiesList[i].Dispose();
			}
		}	
		protected override void OnUpdate()
		{
			var query = GetEntityQuery(typeof(BodyTag));

			bounds.Clear();

			var wipeNodesJob = new WipeNodes()
			{
				nodes = nodes
			};
			var wipeNodesHandle = wipeNodesJob.ScheduleBatch(nodes.Length, nodes.Length / 16);

			var boundJob = new GetBounds()
			{
				limit = 1000,
				bodies = bodies,
				bounds = bounds.AsParallelWriter()
			};
			var boundJobHandler = boundJob.ScheduleBatch(bodies.Length, bodies.Length / 16);

			var finalBound = new NativeArray<Bounds>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
			var finalBoundJob = new FinishGetBounds()
			{
				bounds = bounds,
				finalBound = finalBound,
				limit = 1000,
			};
			var finalBoundHandle = finalBoundJob.Schedule(boundJobHandler);

			for (int i = 0; i < 8; i++)
			{
				bodiesList[i].Clear();
			}
			var sortJob = new SortBodies()
			{
				bodies = bodies,
				bounds = finalBound,
				b0 = bodiesList[0].AsParallelWriter(),
				b1 = bodiesList[1].AsParallelWriter(),
				b2 = bodiesList[2].AsParallelWriter(),
				b3 = bodiesList[3].AsParallelWriter(),
				b4 = bodiesList[4].AsParallelWriter(),
				b5 = bodiesList[5].AsParallelWriter(),
				b6 = bodiesList[6].AsParallelWriter(),
				b7 = bodiesList[7].AsParallelWriter()
			};
			var sortJobHandler = sortJob.ScheduleBatch(bodies.Length, bodies.Length / 16, finalBoundHandle);

			var sortAndWipeHandle = JobHandle.CombineDependencies(sortJobHandler, wipeNodesHandle);

			for (int i = 0; i < 8; i++)
			{
				var generateTreeJob = new GenerateTreePart()
				{
					bodies = bodiesList[i].AsDeferredJobArray(),
					nodes = nodes,
					bounds = finalBound,
					rootIndex = i + 1
				};
				treeJobs[i] = generateTreeJob.Schedule(sortAndWipeHandle);
			}
			var treeJobsHandle = JobHandle.CombineDependencies(treeJobs);

			var rootNodeJob = new GenerateTreeRootNode()
			{
				nodes = nodes,
				finalBound = finalBound
			};
			var rootNodeHandle = rootNodeJob.Schedule(treeJobsHandle);

			var attractAndMoveJob = new AttractAndMoveV2()
			{
				transformHandle = GetComponentTypeHandle<LocalToWorld>(false),
				bodies = bodies,
				deltaForce = Time.DeltaTime * Const.GRAVITY,
				deltaTime = Time.DeltaTime,
				nodes = nodes
			};
			Dependency = attractAndMoveJob.ScheduleParallel(query, JobHandle.CombineDependencies(Dependency, rootNodeHandle));
		}
	}
}