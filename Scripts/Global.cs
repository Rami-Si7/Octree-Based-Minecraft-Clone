	using Godot;
    using SimplexNoise;

	public static class Global
	{
		public static FastNoiseLite noise;
        public static float maxHeight = 0.25f;
        public static bool octreeBreak = false;

		public static float level = 1f;

		public static float GetCaveNoise(int x, int y, int z, float frequency)
		{
			noise.SetNoiseType(FastNoiseLite.NoiseTypeEnum.Simplex); // Use OpenSimplex for smooth caves
			noise.SetFrequency(frequency); // Adjust cave frequency

			return noise.GetNoise3D(x, y, z); // Returns value from -1 to 1
		}
		public static void InitializeNoise()
		{
			noise = new FastNoiseLite();
			noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin; // Smooth terrain
			noise.Seed = 1234; // Consistent terrain generation
			noise.Frequency = 0.02f; // Adjusted for more realistic terrain scale
			noise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm; // Smooth, natural blending
			noise.FractalOctaves = 5; // More detail in terrain
			noise.FractalLacunarity = 2.0f; // Keeps terrain balanced
			noise.FractalGain = 0.5f; // Balanced hills and valleys
		}
        public static float GetNoisePoint(int x, int y ,int z, float noiseScale) 
        {     
            
            float noise = SimplexNoise.Noise.CalcPixel3D(x, y, z, noiseScale);
            // float noisee = noise.GetNoise2D(x,z);
            
            return noise;
        }

	}
