using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace nbody
{
	//[DisableAutoCreation]
	public class GravitySystemShaderBrute : SystemBase
	{
		private NativeArray<Body> bodies;
		private CancellationTokenSource cancelToken;
		private ComputeShader computeShader;
		private ComputeBuffer bodiesBuffer;
		private bool updated = false;

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

						pos.y *= spawnData.diskRadius * 0.5f;
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
			cancelToken = new CancellationTokenSource();
			computeShader = Resources.Load<ComputeShader>("AddForce");
			bodiesBuffer = new ComputeBuffer(bodies.Length, sizeof(float) * 8);
			bodiesBuffer.SetData(bodies);
			_ = Update(cancelToken.Token);
		}
		protected override void OnStopRunning()
		{
			cancelToken.Cancel();
			DisposeAll();
		}
		private void DisposeAll()
		{
			if (bodies.IsCreated)
				bodies.Dispose();
			bodiesBuffer.Release();
		}
		private async Task Update(CancellationToken token)
		{
			await Task.Yield();
			int count = 0;
			var watch = new System.Diagnostics.Stopwatch();
			long ti = 0L;
			while (!token.IsCancellationRequested)
			{
				watch.Reset();
				watch.Start();
				var deltaTime = Time.DeltaTime;
				var result = await RunComputeShader(deltaTime);
				bodies.Dispose();
				bodies = new NativeArray<Body>(result, Allocator.Persistent);
				result.Dispose();
				updated = true;
				watch.Stop();
				ti += watch.ElapsedMilliseconds;
				count++;
				if (count >= 10)
				{
					Debug.Log(ti * 0.1f);
					count = 0;
					ti = 0;
				}
			}
			DisposeAll();
		}
		private async Task<NativeArray<Body>> RunComputeShader(float dt)
		{
			computeShader.SetBuffer(0, "bodies", bodiesBuffer);
			computeShader.SetFloat("deltaTime", dt);
			computeShader.SetInt("bodyCount", bodies.Length);
			computeShader.Dispatch(0, bodies.Length / 256, 1, 1);

			//lots of weird stuff going on here to avoid a unity bug https://forum.unity.com/threads/asyncgpureadback-requestintonativearray-causes-invalidoperationexception-on-nativearray.1011955/
			var taskCompletionSource = new TaskCompletionSource<AsyncGPUReadbackRequest>();
			var tempArray = new NativeArray<Body>(bodies.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
			AsyncGPUReadback.RequestIntoNativeArray(ref tempArray, bodiesBuffer, (req) => taskCompletionSource.SetResult(req));
			await taskCompletionSource.Task;
			return tempArray;
		}
		protected override void OnUpdate()
		{
			if (!updated)
				return;
			var query = GetEntityQuery(typeof(BodyTag));
			var attractAndMoveJob = new DisplayBodies()
			{
				transformHandle = GetComponentTypeHandle<LocalToWorld>(false),
				bd = bodies
			};
			Dependency = attractAndMoveJob.ScheduleParallel(query, Dependency);
			updated = false;
		}
	}
}