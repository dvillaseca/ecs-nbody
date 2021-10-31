using Unity.Mathematics;

namespace nbody
{
	public class Utils
	{
		public static float MassToSize(float mass)
		{
			return math.pow(Const.DENSITY * mass, 0.333333f);
		}
		public static void GetLimit(ref float3 min, ref float3 max, float3 vec)
		{
			max = math.max(max, vec);
			min = math.min(min, vec);
		}
	}
}