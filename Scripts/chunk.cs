using Godot;
using System;
using SimplexNoise;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
[Tool]
public partial class chunk : StaticBody3D
{
	Vector3I chunkPosition = new Vector3I(0,0,0);
	public Octree octree;
	private int chunkSize=4;
	private Texture2D textureAtlas = (Texture2D)ResourceLoader.Load("textures/new_textures_0.1.png");
	public enum VoxelTexture
	{
		Grass,
		SideGrass,
		Dirt,
		Stone,
		Snow,
		SnowSideGrass,
		Wood,
		SideWood,
		Bricks,
		RedBricks,
		BuildingWood,
		SideBuildingWood,
	}
	public enum Face
	{
		Top,
		Bottom,
		Left,
		Right,
		Front,
		Back
	}
	private Dictionary<VoxelTexture, Vector2[]> UVMapping = new Dictionary<VoxelTexture, Vector2[]>();
	private Vector3 playerPosition = new Vector3(0,0,0);
	public MeshInstance3D chunkMesh = new MeshInstance3D();
	[Export]private CollisionShape3D collisionShape = new CollisionShape3D();
	private ConcurrentQueue<Mesh> meshQueue = new ConcurrentQueue<Mesh>();
	SurfaceTool surfaceTool = new();


public Dictionary<Vector3, List<(Vector3[], Face, VoxelTexture)>> chunkTriangles = new Dictionary<Vector3, List<(Vector3[], Face, VoxelTexture)>>();
	// Camera3D editorCamera;
	private int LOD = 1;
	float timeElapsed = 0;
	private bool flag = true;
	private int mesh = 0;

	public override void _Ready()
	{

		// editorCamera = EditorInterface.Singleton.GetEditorViewport3D().GetCamera3D();

		// if(editorCamera == null)
		// {
		// 	GD.Print("camera is null");
		// }
		Global.InitializeNoise();
		PupulateUVMapping();
		

	}
	public Rect2 GetTileRegion(int tileX, int tileY, int tileSize, int atlasSize)
	{
		float uvX = (float)(tileX * tileSize) / atlasSize;
		float uvY = (float)(tileY * tileSize) / atlasSize;
		float uvWidth = (float)tileSize / atlasSize;
		float uvHeight = (float)tileSize / atlasSize;

		return new Rect2(uvX, uvY, uvWidth, uvHeight);
	}
	Vector2[] GetFaceUVs(int tileX, int tileY, int tileSize, int atlasSize)
	{
		
		Rect2 uvRegion = GetTileRegion(tileX, tileY, tileSize, atlasSize);

		return new Vector2[]
		{
						new Vector2(uvRegion.Position.X + uvRegion.Size.X, uvRegion.Position.Y),       // Bottom Right
			new Vector2(uvRegion.Position.X + uvRegion.Size.X, uvRegion.Position.Y + uvRegion.Size.Y), // Top Right
			new Vector2(uvRegion.Position.X, uvRegion.Position.Y + uvRegion.Size.Y),       // Top Left
			new Vector2(uvRegion.Position.X, uvRegion.Position.Y),                         // Bottom Left
		};
	}
	private void PupulateUVMapping()
	{
		UVMapping[VoxelTexture.Grass] = GetFaceUVs(0,0,16,64);
		UVMapping[VoxelTexture.Dirt] = GetFaceUVs(1,0,16,64);
		UVMapping[VoxelTexture.Stone] = GetFaceUVs(2,0,16,64);
		UVMapping[VoxelTexture.SideGrass] = GetFaceUVs(3,0,16,64);
		UVMapping[VoxelTexture.SnowSideGrass] = GetFaceUVs(0,1,16,64);
		UVMapping[VoxelTexture.Snow] = GetFaceUVs(1,1,16,64);
		UVMapping[VoxelTexture.Wood] = GetFaceUVs(2,1,16,64);
		UVMapping[VoxelTexture.SideWood] = GetFaceUVs(3,1,16,64);
		UVMapping[VoxelTexture.Bricks] = GetFaceUVs(0,2,16,64);
		UVMapping[VoxelTexture.RedBricks] = GetFaceUVs(1,2,16,64);
		UVMapping[VoxelTexture.BuildingWood] = GetFaceUVs(2,2,16,64);
		UVMapping[VoxelTexture.SideBuildingWood] = GetFaceUVs(3,2,16,64);

	}
	public void SetChunkPosition(Vector3I position)
	{
		chunkPosition = position;
	}
	public void SetChunkSize(int chunkSize)
	{
		this.chunkSize = chunkSize;
	}


public void UpdateChunk(Octree root, int lod)
{
	// GD.Print("Start mesh generation...");
	World.instance.chunkMeshTasks.Add(Task.Run(() =>
	{
		chunkMesh = new MeshInstance3D();
		SurfaceTool surfaceTool = new SurfaceTool();
		surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
		
		octree.GenerateMeshData(surfaceTool, root, lod, chunkTriangles);
		
		surfaceTool.SetMaterial(new StandardMaterial3D()
		{
			AlbedoTexture = textureAtlas,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		});

		var arrayMesh = surfaceTool.Commit();
		// meshQueue.Enqueue(arrayMesh);
		// GD.Print("adding to queue");
		RefreshChunk();
	}));
}

public void RefreshChunk()
{

	SurfaceTool surfaceTool = new SurfaceTool();
	surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

	foreach (var p in chunkTriangles)
	{

		foreach (var list in p.Value)
		{
			VoxelTexture temp;
			if(list.Item2 == Face.Top &&  list.Item3 == VoxelTexture.SideGrass) 
			{
				temp = VoxelTexture.Grass;
			}
			else if(list.Item2 == Face.Top &&  list.Item3 == VoxelTexture.SnowSideGrass) temp = VoxelTexture.Snow;
			else if(list.Item2 != Face.Top &&  list.Item2 != Face.Bottom &&list.Item3 == VoxelTexture.Wood) temp = VoxelTexture.SideWood;
			else if(list.Item2 != Face.Bottom &&list.Item3 == VoxelTexture.BuildingWood) temp = VoxelTexture.SideBuildingWood;
			else temp = list.Item3;
			Vector2[] k = UVMapping[temp];
			surfaceTool.AddTriangleFan(list.Item1, k);
		}
	}

	surfaceTool.SetMaterial(new StandardMaterial3D()
	{
		// VertexColorUseAsAlbedo = true,
		AlbedoTexture = textureAtlas,
		TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
		CullMode = BaseMaterial3D.CullModeEnum.Disabled
	});

	var arrayMesh = surfaceTool.Commit();
	CallDeferred(nameof(UpdateMeshAndCollision), arrayMesh);
}

private  void UpdateMeshAndCollision(Mesh arrayMesh)
{

	chunkMesh.Mesh = arrayMesh;

	var collision = arrayMesh.CreateTrimeshShape();


	collisionShape.Shape = collision;

	if (!chunkMesh.IsInsideTree())
	{
		CallDeferred("add_child", chunkMesh);
	}

	if (!collisionShape.IsInsideTree())
	{
		CallDeferred("add_child", collisionShape);
	}
}

private float GetReminder(float cooridante)
{
	float result = 0;

	if(cooridante == Mathf.FloorToInt(cooridante))
	{
		return cooridante - 1;
	}
	float remainder = cooridante - Mathf.FloorToInt(cooridante);
	result = cooridante - remainder;

	return result;	
}
private Octree GetVoxelPosition(Vector3 hitPosition, Vector3 normal)
{
	Vector3 voxelPos = new Vector3(
		hitPosition.X,
		hitPosition.Y,
		hitPosition.Z
	);

	if (normal.X > 0) hitPosition.X = GetReminder(hitPosition.X); // Hit Left Face
	if (normal.Y > 0) hitPosition.Y = GetReminder(hitPosition.Y); // Hit Bottom Face
	if (normal.Z > 0) hitPosition.Z = GetReminder(hitPosition.Z); // Hit Back Face
	
	Octree c = octree.FindNeighboursAtSameLevel(hitPosition, 1);
	Octree found = c.FindLeafAtPosition_2(voxelPos, normal);
	if(found != null && found.size < 0.0625)
	{
		return null;
	}

	return found;
}

public void BreakBlock(Vector3 rayCastPosition, Vector3 rayCastNormal)
{
	if(Global.octreeBreak)
	{
		Octree voxel = GetVoxelPosition(rayCastPosition, rayCastNormal);
		if(voxel == null)
		{
			return;
		}
		World.instance.player.highlight.GlobalPosition = voxel.position;
		World.instance.player.highlightScale.X = voxel.position.X;
		World.instance.player.highlightScale.Y = voxel.position.Y;
		World.instance.player.highlightScale.Z = voxel.position.Z;
		World.instance.player.highlight.Scale = World.instance.player.highlightScale;
		octree.RemoveAndSplitBlock(rayCastPosition, rayCastNormal, voxel);
	}
	else
	{
		Vector3I newPosition = new Vector3I(Mathf.FloorToInt(rayCastPosition.X), Mathf.FloorToInt(rayCastPosition.Y), Mathf.FloorToInt(rayCastPosition.Z));
		if (rayCastNormal.X > 0)
		{
			newPosition.X = (int)(rayCastPosition.X - Mathf.FloorToInt(rayCastPosition.X) == 0 ? rayCastPosition.X - 1: Mathf.FloorToInt(rayCastPosition.X));
		}

		if (rayCastNormal.Y > 0)		
		{
			newPosition.Y = (int)(rayCastPosition.Y - Mathf.FloorToInt(rayCastPosition.Y) == 0 ? rayCastPosition.Y - 1: Mathf.FloorToInt(rayCastPosition.Y));
		}
		if (rayCastNormal.Z > 0)
		{
			newPosition.Z = (int)(rayCastPosition.Z - Mathf.FloorToInt(rayCastPosition.Z) == 0 ? rayCastPosition.Z - 1: Mathf.FloorToInt(rayCastPosition.Z));
		}

		octree.RemoveBlock(newPosition);
	}

	RefreshChunk();
}
public void PlaceBlock(Vector3 position, Vector3 normal, VoxelTexture voxelTexture, int xScale, int yScale, int zScale)
{
	int startZ = 0, endZ = 0, startX = 0, endX = 0, startY = 0, endY = 0;
	Vector3I newPosition = new Vector3I(Mathf.FloorToInt(position.X), Mathf.FloorToInt(position.Y), Mathf.FloorToInt(position.Z));
	if (normal.X > 0)
	{
		startX = 0;
		endX = xScale;

		startZ = -Mathf.FloorToInt((zScale )/2);
		endZ = (int)Mathf.Ceil((zScale )/2f);

		startY = -Mathf.FloorToInt((yScale )/2);
		endY = (int)Mathf.Ceil((yScale )/2f);
	}
	if (normal.X < 0)
	{
		newPosition.X -= 1;

		startZ = -Mathf.FloorToInt((zScale )/2);
		endZ = (int)Mathf.Ceil((zScale)/2f);

		startY = -Mathf.FloorToInt((yScale )/2);
		endY = (int)Mathf.Ceil((yScale )/2f);

		startX = -xScale + 1;
		endX = 1;
	}
	if (normal.Y > 0)
	{
		startX = 0;
		endX = xScale;
		startZ = 0;
		endZ = zScale;
		startY = 0;
		endY = yScale;
	}
	if (normal.Y < 0) 
	{
		newPosition.Y -= 1;

		startZ = -zScale + 1;
		endZ = 1;
		startX = -xScale + 1;
		endX = 1;

		startY = -yScale + 1;
		endY = 1;
	}
	if (normal.Z > 0)
	{
		startX = -Mathf.FloorToInt((xScale )/2);
		endX = (int)Mathf.Ceil((xScale)/2f);

		startZ = 0;
		endZ = zScale;

		startY = -Mathf.FloorToInt((yScale )/2);
		endY = (int)Mathf.Ceil((yScale )/2f);
	}
	if (normal.Z < 0) 
	{
		newPosition.Z -= 1; 


		startZ = -zScale + 1;
		endZ = 1;
		startX = -Mathf.FloorToInt((xScale )/2);
		endX = (int)Mathf.Ceil((xScale )/2f);

		startY = -Mathf.FloorToInt((yScale )/2);
		endY = (int)Mathf.Ceil((yScale )/2f);
		
	}

	Vector3 temp = new Vector3(0,0,0);
	for(int x = startX ; x < endX; x++)
	{
		for(int y = startY; y < endY; y++)
		{
			for(int z = startZ; z < endZ; z++)
			{
				temp.X = x + newPosition.X;
				temp.Y =  y +newPosition.Y;
				temp.Z = z + newPosition.Z;
				chunk chunk = World.instance.GetChunkAt(temp);
				if(chunk != null)
				{
					if(chunk == this)
					{
						octree.AddBlock(temp - chunk.GlobalTransform.Origin, voxelTexture);
					}
					else
					{
						chunk.octree.AddBlock(temp - chunk.GlobalTransform.Origin, voxelTexture);
						Task.Run(() => chunk.RefreshChunk());
						Task.Delay(100);
					}
				}
				temp = Vector3.Zero;
			}
		}
	}
	Task.Run(() => RefreshChunk());
}


// Generate the octree in a background thread

	public Octree GenerateOctree()
	{
		Octree newOctree = new Octree(this);
		newOctree.position = chunkPosition;
		newOctree.desiredLODLevel = chunkSize;
		newOctree.size = chunkSize;
		newOctree.chunkInstance = this;

		// Perform subdivision in a background thread
		newOctree.Divide(LOD, chunkPosition);
		// DivideOctreeInParallel(newOctree,playerPosition, heightMap, Global.maxHeight, LOD, chunkPosition);

		return newOctree;
	}
	public void IncreaseLOD(int newLOD)
	{
		if (octree.desiredLODLevel > newLOD)
		{
			GD.Print("increase lod");
			octree.desiredLODLevel = newLOD;
			octree.Divide(newLOD, chunkPosition);
			// CleanChunkMesh();
			UpdateChunk(octree, newLOD);
		}
	}

	public void ReduceLOD(int newLOD)
	{
		if (octree.desiredLODLevel < newLOD)
		{
			octree.desiredLODLevel = newLOD;
			GD.Print("decrease lod");
			CleanChunkMesh();
			UpdateChunk(octree,newLOD);
		}
	}
	public override void _Process(double delta)
	{
	}
	private void CleanChunkMesh()
	{
		if (chunkMesh.Mesh != null)
		{
			chunkMesh.CallDeferred("queue_free");
			chunkMesh = new MeshInstance3D();
			GD.Print("free mesh");
		}

		if (collisionShape.Shape != null)
		{
			collisionShape.CallDeferred("queue_free");
			collisionShape = new CollisionShape3D();
			GD.Print("free collision");
		}
	}
	
	
		
}
