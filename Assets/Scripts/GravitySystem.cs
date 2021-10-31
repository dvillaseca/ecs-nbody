using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;

namespace nbody
{
	public class GravitySystem : SystemBase
	{
		private NativeQueue<Bounds> bounds;
		
		protected override void OnStartRunning()
		{
			bounds = new NativeQueue<Bounds>(Allocator.Persistent);

			var spawnData = GetSingleton<EntitySpawnData>();
			float explosionDiameter = math.pow(spawnData.count * math.pow(Utils.MassToSize(spawnData.massRange.y), 3f), 0.3333333f);
			for (int i = 0; i < spawnData.count; i++)
			{
				var newEntity = EntityManager.Instantiate(spawnData.prefab);
				float mass = UnityEngine.Random.Range(spawnData.massRange.y, spawnData.massRange.x);
				var pos = UnityEngine.Random.insideUnitSphere;
				pos *= explosionDiameter;
				EntityManager.AddComponent<Body>(newEntity);
				EntityManager.SetComponentData(newEntity, new Body { velocity = pos * spawnData.radius, mass = mass, position = pos });
			}
		}
		protected override void OnStopRunning()
		{
			if (bounds.IsCreated)
				bounds.Dispose();
		}
		protected override void OnUpdate()
		{
			var query = GetEntityQuery(typeof(Body));
			var bodies = query.ToComponentDataArrayAsync<Body>(Allocator.TempJob, out JobHandle dep);
			var deps = JobHandle.CombineDependencies(dep, Dependency);

			bounds.Clear();
			var boundJob = new GetBounds()
			{
				limit = 1000,
				bodies = bodies,
				bounds = bounds.AsParallelWriter()
			};
			var boundJobHandler = boundJob.ScheduleBatch(bodies.Length, bodies.Length / 16, deps);
			var finalBound = new NativeArray<Bounds>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			var finalBoundJob = new FinishGetBounds()
			{
				bounds = bounds,
				finalBound = finalBound,
				limit = 1000,
			};
			var finalBoundHandle = finalBoundJob.Schedule(boundJobHandler);

			var nodes = new NativeArray<LinearOctNode>(800000, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			var generateTreeJob = new GenerateTree()
			{
				bodies = bodies,
				nodes = nodes,
				bounds = finalBound,
				current = 0,
			};
			var generateTreeHandle = generateTreeJob.Schedule(finalBoundHandle);

			var deltaTime = Time.DeltaTime;
			var deltaForce = Const.GRAVITY * deltaTime;
			var attractAndMoveJob = new AttractAndMove()
			{
				bodyHandle = GetComponentTypeHandle<Body>(false),
				transformHandle = GetComponentTypeHandle<LocalToWorld>(false),
				deltaTime = deltaTime,
				deltaForce = deltaForce,
				nodes = nodes
			};
			Dependency = attractAndMoveJob.ScheduleParallel(query, generateTreeHandle);
		}
	}
}