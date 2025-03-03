using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
[Tool]

public partial class World : Node
{
	public static Camera3D editorCamera;
	[Export] public player player;
	public int chunkSize = 32;
	private int worldWidth = 3;
	private int worldHeight = 1;
	private int loadRadius = 2;
	private int unloadRadius = 10;
	[Export] public PackedScene chunkScene { get; set; }
	private Dictionary<Vector3, chunk> chunks;

	private Queue<Vector3> chunkLoadQueue = new Queue<Vector3>();
	public static World instance;
	private Queue<chunk> chunkPool = new Queue<chunk>();
	public int initialPoolSize = 20;
	private int chunksPerFrame = 4; // Number of chunks to load per frame
	private int loadInterval = 3; // Load chunks every 4 frames
	private int unloadInterval = 4; // Load chunks every 4 frames
	private int frameCounter = 0;
	private ConcurrentQueue<Tuple<Octree, Vector3I>> chunkQueue = new ConcurrentQueue<Tuple<Octree, Vector3I>>();
	private ConcurrentQueue<chunk> meshChunk = new ConcurrentQueue<chunk>();
 	private Vector3I lastPlayerChunkCoordinates = new Vector3I(0,0,-1);
	private int chunksMovedCount = 0;
	public int chunkUpdateThreshold = 5; // Update every 5 chunks
	private bool JustStarted = true;
	private bool isWorldReady = false;
	public List<Task> chunkMeshTasks = new List<Task>();
	[Export]private Label fps;
	[Export]public  Label xAxisLabel;
	[Export]public Label yAxisLabel;

	[Export]public Label zAxisLabel;
	[Export]public Slider xSlider;
	[Export]public Slider ySlider;
	[Export]public Slider zSlider;
	[Export] private Button button;
	[Export] public VBoxContainer vBoxContainer;

	private Vector3I currentChunkPosition;


	//////////////// ////////////// //////////////// ////////////////////  ///////////////// /////////////
				////////  create height map for nodes that must be rendered     ////////
	//////////////// ////////////// //////////////// ///////////////////////////// /////////////

	private bool [, ,] PopulateHeightMap(Vector3I chunkPosition)
	{
		bool [, ,] heightGrid = new bool[chunkSize,chunkSize,chunkSize];
		for(int x = 0; x < chunkSize; x++)
		{
			for(int y = 0; y < chunkSize; y++)
			{
				for(int z = 0; z < chunkSize; z++)
				{
					float noise = Global.GetNoisePoint((int)x + chunkPosition.X, (int)y + chunkPosition.Y,(int)z + chunkPosition.Z, 0.008f);
					noise = (noise + 1) / 2;
					float currHeight = noise * Global.maxHeight;
					
					if(y + chunkPosition.Y <= currHeight)
					{
						heightGrid[x,y,z] = true;
					}
					else
					{
						heightGrid[x,y,z] = false;
					}
				}
			}
		}
		return heightGrid;
	}


	public override void _Ready()
	{
		instance = this;
		player = GetNodeOrNull<player>("Player");
		if(!Engine.IsEditorHint())
		{
			lastPlayerChunkCoordinates = new Vector3I(Mathf.FloorToInt(player.GlobalPosition.X)/ chunkSize, Mathf.FloorToInt(player.GlobalPosition.Y)/ chunkSize, Mathf.FloorToInt(player.GlobalPosition.Z)/ chunkSize);

		}
		// editorCamera = EditorInterface.Singleton.GetEditorViewport3D().GetCamera3D();
		chunks = new Dictionary<Vector3, chunk>();
		Global.InitializeNoise();
		GenerateWorld();
		JustStarted = false;
	}




//////////////// ////////////// //////////////// ////////////////////  ///////////////// //////////
				////////  Generate world based on X and Z axis   ////////
		////////  Using MultiThreading to divide the works on all cores   ////////
//////////////// ////////////// //////////////// ///////////////////////////// /////////////
private async void GenerateWorld()
{
	GD.Print("\nGenerating world with multi-threading...");

	List<Task> chunkTasks = new List<Task>();

	for (int x = 0; x < worldWidth; x++)
	{
		// for(int y = 0; y < worldHeight; y++)
		// {
			for (int z = 0; z < worldWidth; z++)
			{
				Vector3I chunkPosition = new Vector3I(x * chunkSize / 2, 0, z * chunkSize / 2);
				chunkTasks.Add(Task.Run(() => GenerateChunkData(chunkPosition, 1)));
				_ = Task.Delay(50);
			}
		// }
	}

	await Task.WhenAll(chunkTasks);
	ApplyGeneratedChunks(); // apply proccessed data and add to scene

	CallDeferred(nameof(ApplyMesh)); // add mesh
	CallDeferred(nameof(FinishWorldGenerating));
	
}
private void FinishWorldGenerating()
{
	isWorldReady = true;
	if(player != null)
	{
		player.canMove = true;
	}
}

private void GenerateChunkData(Vector3I chunkPosition, float lod)
{
	Octree octree = new Octree();
	octree.position = chunkPosition;
	octree.size = chunkSize;
	octree.desiredLODLevel = lod;

	// bool[, ,] heightMap = PopulateHeightMap(chunkPosition);
	// octree.BuildSparseVoxelOctree(lod, heightMap, chunkPosition);

	octree.Divide(lod, chunkPosition); 

	
	chunkQueue.Enqueue(new Tuple<Octree, Vector3I>(octree, chunkPosition));
}

	private void ApplyGeneratedChunks()
	{
		while (chunkQueue.TryDequeue(out var chunkData))
		{
			Octree octree = chunkData.Item1;
			Vector3I chunkPosition = chunkData.Item2;

			var chunkNode = chunkScene.Instantiate<chunk>();
			chunkNode.GlobalTransform = new Transform3D(Basis.Identity, chunkPosition);

			octree.chunkInstance = chunkNode;
			chunkNode.octree = octree;
			AddChild(chunkNode);
			chunks.Add(chunkPosition, chunkNode);
			meshChunk.Enqueue(chunkNode);
		}		
	}
	private void ApplyMesh()
	{
		while(meshChunk.TryDequeue(out var chunkmesh))
		{
			Task.Run(()=>chunkmesh.UpdateChunk(chunkmesh.octree, 1));
			Task.Delay(30);
		}
	}
	public chunk GetChunkAt(Vector3 globalPos)
	{
		// Calculate the chunk position in the world
		Vector3 chunkPos = new Vector3(
			Mathf.FloorToInt(globalPos.X / chunkSize) * chunkSize/2,
			Mathf.FloorToInt(globalPos.Y / chunkSize) * chunkSize/2,
			Mathf.FloorToInt(globalPos.Z / chunkSize) * chunkSize/2
		);

		// Check if the chunk exists in the dictionary
		if (chunks.ContainsKey(chunkPos))
		{
			return chunks[chunkPos];
		}

		return null; // No chunk at this position
	}

	private void UpdateChunks(Vector3 playerPosition)
	{
		Vector3I playerChunkPosition = new Vector3I(Mathf.FloorToInt(playerPosition.X)/ chunkSize, Mathf.FloorToInt(playerPosition.Y)/ chunkSize, Mathf.FloorToInt(playerPosition.Z)/ chunkSize);
		if(!playerChunkPosition.Equals(lastPlayerChunkCoordinates))
		{
			_ = LoadChunks(playerChunkPosition);
		}
		lastPlayerChunkCoordinates = playerChunkPosition;
	}
	private async Task LoadChunks(Vector3I playerChunkPosition)
	{
		List<Task> chunkTasks = new List<Task>();
		GD.Print("\n\n");
		for(int x = -loadRadius; x < loadRadius ; x++)
		{
			// for(int y = 0; y < worldHeight ; y++)
			// {
				for(int z = -loadRadius; z < loadRadius; z++)
				{
					Vector3I newChunkPosition = new Vector3I(x + playerChunkPosition.X,  0, z + playerChunkPosition.Z);
					newChunkPosition.X *= chunkSize/2; 
					newChunkPosition.Y *= chunkSize/2; 
					newChunkPosition.Z *= chunkSize/2; 
					if(!chunks.ContainsKey(newChunkPosition))
					{
						GD.Print($"new chunk to add: position: {newChunkPosition}");
						chunkTasks.Add(Task.Run(()=> GenerateChunkData(newChunkPosition, 1)));
					}
				_ = Task.Delay(50);
				}
			// }
		}
		await Task.WhenAll(chunkTasks);
		ApplyGeneratedChunks();

		ApplyMesh();
	}
private List<Vector3I> GetSurroundingChunks(Vector3I center)
{
	List<Vector3I> res =  new List<Vector3I>
	{
		new Vector3I((center.X - 1)*chunkSize/2, 0, center.Z * chunkSize/2), // Left
		new Vector3I((center.X - 1) * chunkSize/2, 0, (center.Z - 1) * chunkSize/2),
		new Vector3I(center.X * chunkSize/2, 0, (center.Z - 1)*chunkSize/2), // Below
		new Vector3I((center.X + 1) * chunkSize/2, 0, center.Z * chunkSize/2), // Right
		new Vector3I((center.X + 1) * chunkSize/2, 0, (center.Z - 1) * chunkSize/2),
		new Vector3I((center.X)*chunkSize/2, 0, (center.Z + 1) * chunkSize/2), // Above
		new Vector3I((center.X - 1) * chunkSize/2, 0, (center.Z + 1)*chunkSize/2),
		new Vector3I((center.X + 1)*chunkSize/2, 0, (center.Z + 1)*chunkSize/2)
	};
	return res;
}


//////////////// ////////////// //////////////// ////////////// ////////////// ///////////
							// Future Work//
//////////////// ////////////// //////////////// ////////////// ////////////// ///////////
	public void UpdateChunkLOD(Vector3I newChunkPosition)
	{
		if (newChunkPosition.X == currentChunkPosition.X && newChunkPosition.Z == currentChunkPosition.Z)
			return; 

		
		List<Vector3I> previousLOD1Chunks = GetSurroundingChunks(currentChunkPosition);
		List<Vector3I> newLOD1Chunks = GetSurroundingChunks(newChunkPosition);
		

	
		foreach (var chunkPos in previousLOD1Chunks)
		{
			if (chunks.ContainsKey(chunkPos) && !newLOD1Chunks.Contains(chunkPos))
			{
				if(newChunkPosition.X != chunkPos.X && newChunkPosition.Z != chunkPos.Z)
				{
					chunks[chunkPos].ReduceLOD(4);
				}
			}
		}

		
		foreach (var chunkPos in newLOD1Chunks)
		{
			if (chunks.ContainsKey(chunkPos))
			{
				if(chunks[chunkPos].octree.desiredLODLevel != 1)
				{
					Task.Run(()=>chunks[chunkPos].IncreaseLOD(1));
					
				}

			}
		}

		currentChunkPosition = newChunkPosition;
	}

	public override void _PhysicsProcess(double delta)
	{
		if(!Engine.IsEditorHint())
		{

			if(!JustStarted)
			{
				int fps = (int)Engine.GetFramesPerSecond();
				this.fps.Text = $"FPS: {fps}\nPlayer Position: {player.GlobalPosition}\nCollision Normal: {player.rayCast3D.GetCollisionNormal()}";
				UpdateChunks(player.GlobalPosition);
			}
		}
	}
}
