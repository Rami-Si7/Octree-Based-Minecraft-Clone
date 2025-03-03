using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
[Tool]


public class Octree
{
	public Octree[] children {get;set;} = null;
	public Vector3 position{get;set;}
	public Vector3I chunkPosition;
	public float size;
	
	public int maxLevel = 1;
	public chunk chunkInstance;

	public float desiredLODLevel;
	public enum VoxelType
	{
		Air,    // Represents empty space
		Solid,  // Represents stone block
	}
	public chunk.VoxelTexture texture;
	bool isSolid = true;
	

	private static readonly int[] _top = new int[] { 2, 3, 7, 6 };
	private static readonly int[] _bottom = new int[] { 0, 4, 5, 1 };
	private static readonly int[] _left = new int[] { 6, 4, 0, 2 };
	private static readonly int[] _right = new int[] { 3, 1, 5, 7 };
	private static readonly int[] _back = new int[] { 7, 5, 4, 6 };
	private static readonly int[] _front = new int[] { 2, 0, 1, 3 };
	int[][] faces = new int[][]
	{
		_top, // Top
		_bottom, // Bottom
		_left, // Left
		_right, // Right
		_front, // Front
		_back  // Back
	};
	private static Vector3[] _vertices;
	VoxelType voxelType = VoxelType.Air;
	private Dictionary<Vector3I, bool[]> precomputedVisibility = new Dictionary<Vector3I, bool[]>();

	public Octree()
	{
		
	}
	public Octree(chunk chunk)
	{
		chunkInstance = chunk;
	}

////////////////////////////////////////////////////////////////////////////////////////
		/////// This section is resposible for building SparseVoxelOctree //////
////////////////////////////////////////////////////////////////////////////////////////
	public void BuildSparseVoxelOctree(float desiredLODLevel, bool [, ,] heightMap, Vector3I chunkPosition)
	{
		if(size == desiredLODLevel)
		{
			bool isSolid = heightMap[(int)position.X - chunkPosition.X, (int)position.Y - chunkPosition.Y, (int)position.Z- chunkPosition.Z];
			voxelType = isSolid ? VoxelType.Solid : VoxelType.Air;
			return;
		}
		float minX = position.X;
		float maxX = position.X + size;
		float minY = position.Y;
		float maxY = position.Y + size;
		float minZ = position.Z;
		float maxZ = position.Z + size;

		bool allSolid = true, allAir = true;
		for(float x = minX; x < maxX; x++)
		{
			for(float y = minY; y < maxY; y++)
			{
				for(float z = minZ; z < maxZ; z++)
				{
					bool isSolid = heightMap[(int)x - chunkPosition.X, (int)y - chunkPosition.Y, (int)z-chunkPosition.Z];
					allSolid &= isSolid;
					allAir &= !isSolid;
					if(!allAir && !allSolid) break;
					
				}
				if(!allAir && !allSolid) break;
			}
			if(!allAir && !allSolid) break;
		}
		if(allSolid || allAir)
		{
			voxelType = allSolid ? VoxelType.Solid : VoxelType.Air;

		}
		else
		{
			children = new Octree[8];
			for(int i = 0; i < 8; i++)
			{
				children[i] = new Octree();
			}
			float offSet = size/2;
			foreach(var c in children)
			{
				c.size = size/2;
			}
			float x = position.X, y = position.Y, z = position.Z;
			children[0].position = new Vector3(x, y, z);
			children[1].position = new Vector3(x + offSet, y, z);
			children[2].position = new Vector3(x, y + offSet, z);
			children[3].position = new Vector3(x + offSet, y + offSet, z);
			children[4].position = new Vector3(x, y, z + offSet);
			children[5].position = new Vector3(x + offSet, y, z + offSet);
			children[6].position = new Vector3(x, y + offSet, z + offSet);
			children[7].position = new Vector3(x + offSet, y + offSet, z + offSet);
			foreach(var c in children)
			{
				c.BuildSparseVoxelOctree(desiredLODLevel, heightMap, chunkPosition);
			}

		}
	}


	private void GetVisibleFacesOfSparseVoxelOctree(Octree root)
	{
		bool[] visibleFaces = new bool[6] { true, true, true, true, true, true };

		Vector3[] neighborOffsets = new Vector3[]
		{
			new Vector3(0, size, 0),  // Top
			new Vector3(0, -size, 0),  // Bottom
			new Vector3(-size, 0, 0), // Left
			new Vector3(size, 0, 0),  // Right
			new Vector3(0, 0, size),  // Front
			new Vector3(0, 0, -size), // Back
		};
		
		Octree node = root.FindLeafAtPosition(position);
		for(int i = 0; i < 6; i++)
		{
			Vector3 neighborPos = (Vector3)(this.position + neighborOffsets[i]);
			Octree neighbour = root.FindLeafAtPosition(neighborPos);
			if(neighbour != null)
			{
				if(neighbour.size > size)
				{

				}
			}
		}
	}
	//////////////////////////////////////////////////////////////////////////////
	//////////////////////////////////////////////////////////////////////////////
	//////////////////////////////////////////////////////////////////////////////
	

	//////////////////////////////////////////////////////////////////////////////
		////////     HERE IS WORLD BUILD FROM OCTREES (NOT SPARSE) ///////////
	//////////////////////////////////////////////////////////////////////////////
	public void SetTexture(float y, float height)
	{
		if(y < (int)height/2)
		{
			texture = chunk.VoxelTexture.Stone;
		}
		 else if(y <(int)height)
		{
			texture = chunk.VoxelTexture.Dirt;
		}
		else if(y == (int)height)
		{
			texture = chunk.VoxelTexture.SideGrass;
		}
		
	}
	
		///////////////////////////  For dividing nodes  ////////////////////////////////
	public void Divide(float desiredLODLevel, Vector3I chunkposition)
	{
		float x = position.X, y = position.Y, z = position.Z;

		if(size == desiredLODLevel)
		{
	
			if(chunkposition.Y >= 0)
			{
				float noiseValue = Global.GetNoisePoint((int)x + chunkposition.X, (int)y + chunkposition.Y,(int)z + chunkposition.Z, 0.02f);
				float normalizedNoiseValue = (noiseValue + 1) / 2;
				float currHeight = normalizedNoiseValue * Global.maxHeight;
				float caveNoise = Global.GetCaveNoise((int)x + chunkposition.X, (int)y, (int)z + chunkposition.Z, 0.1f);		
				if( y + chunkposition.Y <= currHeight)
				{
					voxelType = VoxelType.Solid;
					SetTexture(y + chunkposition.Y, currHeight);
					
				}
				else
				{
					voxelType = VoxelType.Air;
				}
			}
			else
			{
				float caveNoise = Global.GetCaveNoise((int)x + chunkposition.X, (int)y, (int)z + chunkposition.Z, 0.02f);
				if(caveNoise < -0.05 || y ==chunkPosition.Y)
				{
					voxelType = VoxelType.Solid;
					texture = chunk.VoxelTexture.Stone;
					
				}
				else
				{
					voxelType = VoxelType.Air;
				}
			}
			return;
		}
	
		children = new Octree[8];
		for(int i = 0; i < 8; i++)
		{
			children[i] = new Octree();
		}
		float offSet = size/2;
		foreach(var c in children)
		{
			c.size = size/2;
		}
		children[0].position = new Vector3(x, y, z);
		children[1].position = new Vector3(x + offSet, y, z);
		children[2].position = new Vector3(x, y + offSet, z);
		children[3].position = new Vector3(x + offSet, y + offSet, z);
		children[4].position = new Vector3(x, y, z + offSet);
		children[5].position = new Vector3(x + offSet, y, z + offSet);
		children[6].position = new Vector3(x, y + offSet, z + offSet);
		children[7].position = new Vector3(x + offSet, y + offSet, z + offSet);
		
		foreach(var c in children)
		{
			c.Divide(desiredLODLevel, chunkposition);
		}
	}

	///////////// for future feature ////////////
	public void DivideToPlace(float desiredLODLevel, Vector3 placePosition)
	{
		if(size == desiredLODLevel)
		{
			voxelType = VoxelType.Solid;
			return;
		}
		if(children == null)
		{
			float x = position.X, y = position.Y, z = position.Z;
			children = new Octree[8];
			for(int i = 0; i < 8; i++)
			{
				children[i] = new Octree();
			}
			float offSet = size/2;
			foreach(var c in children)
			{
				c.size = size/2;
				// c.desiredLODLevel = offSet;
			}
			children[0].position = new Vector3(x, y, z);
			children[1].position = new Vector3(x + offSet, y, z);
			children[2].position = new Vector3(x, y + offSet, z);
			children[3].position = new Vector3(x + offSet, y + offSet, z);
			children[4].position = new Vector3(x, y, z + offSet);
			children[5].position = new Vector3(x + offSet, y, z + offSet);
			children[6].position = new Vector3(x, y + offSet, z + offSet);
			children[7].position = new Vector3(x + offSet, y + offSet, z + offSet);
		}
		foreach(Octree c in children)
		{
			if(c.Contains(placePosition, c.size))
			{
				c.DivideToPlace(desiredLODLevel, placePosition);
			}
		}
	}


	public bool Contains(Vector3 pos, float size)
	{
		return pos.X >= position.X && pos.X < position.X + size &&
			   pos.Y >= position.Y && pos.Y < position.Y + size &&
			   pos.Z >= position.Z && pos.Z < position.Z + size;
	}


		///////////////////       This code take care of generating mesh for rendered nodes   //////////////////////////////
	public void GenerateMeshData(SurfaceTool surfaceTool, Octree root, int lod, Dictionary<Vector3, List<(Vector3[], chunk.Face, chunk.VoxelTexture)>> chunkTriangles)
	{
		
		Queue<Octree> nodesToProcess = new Queue<Octree>();
		nodesToProcess.Enqueue(this);
		
		while (nodesToProcess.Count > 0)
		{
			Octree node = nodesToProcess.Dequeue();

			if (node.children == null || node.size == lod)
			{
				if (node.voxelType == VoxelType.Air) continue; // Skip air nodes
				bool[] visibleFaces = node.GetVisibleFaces(root);
		
				if(!chunkTriangles.ContainsKey(node.position))
				{
					chunkTriangles[node.position] = new List<(Vector3[], chunk.Face, chunk.VoxelTexture)>();
				}

				node.GenerateTriangles(chunkTriangles[node.position], visibleFaces, node.texture);
			}
			else
			{
				foreach (var child in node.children)
				{
					nodesToProcess.Enqueue(child);
				}
			}
		}
		foreach(var p in chunkTriangles)
		{
			foreach (var triangle in p.Value)
			{
				// GD.Print("adding triangles");
				surfaceTool.AddTriangleFan(triangle.Item1);
			}
		}
	}
private void GenerateTriangles(List<(Vector3[], chunk.Face, chunk.VoxelTexture)> triangleData, bool[] visibleFaces, chunk.VoxelTexture type)
{
	float x = this.position.X;
	float y = this.position.Y;
	float z = this.position.Z;
	float offSet = size;
	// GD.Print("offset:", offSet);

	Vector3[] _vertices = new Vector3[]
	{
		new Vector3(x, y, z),                   // 0: Bottom-left-front
		new Vector3(x + offSet, y, z),           // 1: Bottom-right-front
		new Vector3(x, y + offSet, z),           // 2: Top-left-front
		new Vector3(x + offSet, y + offSet, z),  // 3: Top-right-front
		new Vector3(x, y, z + offSet),           // 4: Bottom-left-back
		new Vector3(x + offSet, y, z + offSet),  // 5: Bottom-right-back
		new Vector3(x, y + offSet, z + offSet),  // 6: Top-left-back
		new Vector3(x + offSet, y + offSet, z + offSet)   // 7: Top-right-back
	};


	if (visibleFaces[0]) triangleData.Add((new Vector3[] { _vertices[2], _vertices[3], _vertices[7], _vertices[6] }, chunk.Face.Top, type)); // Top
	if (visibleFaces[1]) triangleData.Add((new Vector3[] { _vertices[0], _vertices[4], _vertices[5], _vertices[1] }, chunk.Face.Bottom, type)); // Bottom
	if (visibleFaces[2]) triangleData.Add((new Vector3[] { _vertices[6], _vertices[4], _vertices[0], _vertices[2] }, chunk.Face.Left, type)); // Left
	if (visibleFaces[3]) triangleData.Add((new Vector3[] { _vertices[3], _vertices[1], _vertices[5], _vertices[7] }, chunk.Face.Right, type)); // Right
	if (visibleFaces[5]) triangleData.Add((new Vector3[] { _vertices[2], _vertices[0], _vertices[1], _vertices[3] }, chunk.Face.Back, type)); // Front
	if (visibleFaces[4]) triangleData.Add((new Vector3[] { _vertices[7], _vertices[5], _vertices[4], _vertices[6] }, chunk.Face.Front, type)); // Back
}


/////////////////////////////////////////////////////////////////////
			// this code remove only one block //
////////////////////////////////////////////////////////////////////////
public void RemoveBlock(Vector3 position)
{
	
	Octree node = FindNeighboursAtSameLevel(position, 1);
	if(node == null)
	{
		return;
	}

	float x = position.X, y = position.Y, z = position.Z;
	Vector3[] faces = new Vector3[]
	{
		new Vector3(x, y + 1, z),
		new Vector3(x, y - 1, z),
		new Vector3(x - 1, y, z),
		new Vector3(x + 1, y, z),
		new Vector3(x, y, z + 1),
		new Vector3(x, y, z - 1)
	};
	for(int i = 0; i < 6; i++)
	{
		Octree res = FindLeafAtPosition(faces[i]);
		if(res != null && res.voxelType != VoxelType.Air)
		{
			node.voxelType = VoxelType.Air;
			AddFaceToNeighbour(faces[i], i, res.size, res.texture);
		}
		
	}
	// chunkInstance.chunkTriangles[position].Clear();
	HandleFacesOfDeletedBlcok(node);
	node.ClearVoxels(this);


}
private void ClearVoxels(Octree octree)
{
	if(children == null)
	{
		if(octree.chunkInstance.chunkTriangles.ContainsKey(position))
		{
			octree.chunkInstance.chunkTriangles[position].Clear();
		}
		return;
	}
	for(int i = 0; i < 8; i++)
	{
		children[i].ClearVoxels(octree);
		children[i] = null;
	}
	children = null;
}

///////////////////////////////////////////////////////////////////////////////////////////////////
			// this code split the block and remove octant at hit position //
//////////////////////////////////////////////////////////////////////////////////////////////////////

	public void RemoveAndSplitBlock(Vector3 raycastPosition, Vector3 normal, Octree voxel)
	{
		if(voxel.size == Global.level)
		{
			voxel.voxelType = VoxelType.Air;
			chunkInstance.chunkTriangles[voxel.position].Clear();
			HandleFacesOfDeletedBlcok(voxel);
			return;
		}
		voxel.Divide(voxel.size/2, (Vector3I)chunkInstance.Position);
		Octree temp = null;
		foreach(Octree c in voxel.children)
		{
			Vector3 s;
			if(normal.X > 0 || normal.Y > 0 || normal.Z > 0)
			{
				s = raycastPosition - c.size * normal;
			}
			else
			{
				s = raycastPosition;
			}
			if(c.Contains(s, c.size))
			{
				c.voxelType = VoxelType.Air;
				temp = c;
			}
			else
			{
				c.voxelType = VoxelType.Solid;
				c.texture = voxel.texture;
			}
		}
		chunkInstance.chunkTriangles[voxel.position].Clear();

		foreach(Octree c in voxel.children)
		{
			if(c.voxelType == VoxelType.Air) continue;
			bool [] visibleFaces = c.GetVisibleFaces(this);
			if(!chunkInstance.chunkTriangles.ContainsKey(c.position))
			{
				chunkInstance.chunkTriangles[c.position] = new List<(Vector3 [], chunk.Face, chunk.VoxelTexture)>();
			}
			c.GenerateTriangles(chunkInstance.chunkTriangles[c.position], visibleFaces, c.texture);
		}
		HandleFacesOfDeletedBlcok(temp);
	}
	private void HandleFacesOfDeletedBlcok(Octree node)
	{
		float x = node.position.X, y = node.position.Y, z = node.position.Z;
		float offSet = node.size;
		Vector3[] faces = new Vector3[]
		{
			new Vector3(x, y + offSet, z),
			new Vector3(x, y - offSet, z),
			new Vector3(x - offSet, y, z),
			new Vector3(x + offSet, y, z),
			new Vector3(x, y, z + offSet),
			new Vector3(x, y, z - offSet)
		};
		Dictionary<int,int> mapped = new Dictionary<int, int>()
		{
			{0,1},
			{1,0},
			{2,3},
			{3,2},
			{4,5},
			{5,4}
		};
		for(int i = 0; i < 6; i++)
		{
			Octree res = chunkInstance.octree.FindNeighboursAtSameLevel(faces[i] ,node.size);

			if(res != null)
			{
				
				if(res.children == null)
				{
					if(res.size >= node.size && res.voxelType != VoxelType.Air)
					{
						AddFaceToNeighbour(res.position, i, res.size, res.texture);
					}
					else if(res.size < node.size  && res.voxelType != VoxelType.Air)
					{
						AddFaceToNeighbour(res.position, i, res.size, res.texture);
					}
				}
				else
				{
					Queue<Octree> queue = new Queue<Octree>();
					queue.Enqueue(res);
					while(queue.Count > 0)
					{
						Octree tmp = queue.Dequeue();
						foreach(var c in tmp.children)
						{
							if(c.children == null && c.voxelType != VoxelType.Air)
							{
								AddFaceToNeighbour(c.position, i, c.size, c.texture);
							}
							else if(c.children != null)
							{
								queue.Enqueue(c);
							}
						}
					}
				}
			}
		}
	}

////////////////////////////////////////////////////////////////////////
		// This code resposible for adding one block   //
////////////////////////////////////////////////////////////////////////
	public void AddBlock(Vector3 position, chunk.VoxelTexture voxelTexture)
	{
		Octree curr_pos = FindLeafAtPosition(position);
		if(curr_pos.voxelType == VoxelType.Solid)
		{
			return;
		}
		float x = position.X, y = position.Y, z = position.Z;
		Vector3[] faces = new Vector3[]
		{
			new Vector3(x, y + 1, z),
			new Vector3(x, y - 1, z),
			new Vector3(x - 1, y, z),
			new Vector3(x + 1, y, z),
			new Vector3(x, y, z + 1),
			new Vector3(x, y, z - 1)
		};
		Dictionary<int,int> mapped = new Dictionary<int, int>()
		{
			{0,1},
			{1,0},
			{2,3},
			{3,2},
			{4,5},
			{5,4}
		};
		
		for(int i = 0; i < 6; i++)
		{
			Octree res = FindLeafAtPosition(faces[i]);
			if(res != null && res.voxelType != VoxelType.Air)
			{
				RemoveFace(faces[i], i);
			}
		}
		curr_pos.voxelType = VoxelType.Solid;
		bool [] visibleFaces = curr_pos.GetVisibleFaces(this);
		// GD.Print($" visible faces: {visibleFaces}");
		if(!chunkInstance.chunkTriangles.ContainsKey(curr_pos.position))
		{
			chunkInstance.chunkTriangles[curr_pos.position] = new List<(Vector3[], chunk.Face, chunk.VoxelTexture)>();
		}
		curr_pos.texture = voxelTexture;
		curr_pos.GenerateTriangles(chunkInstance.chunkTriangles[curr_pos.position], visibleFaces, voxelTexture);

	}


	private void AddFaceToNeighbour(Vector3 position, int index, float size, chunk.VoxelTexture type)
	{
		float x = position.X, y = position.Y, z = position.Z;
		Vector3[] _vertices = new Vector3[]
		{
			new Vector3(x, y, z),                   // 0: Bottom-left-front
			new Vector3(x + size, y, z),           // 1: Bottom-right-front
			new Vector3(x, y + size, z),           // 2: Top-left-front
			new Vector3(x + size, y + size, z),  // 3: Top-right-front
			new Vector3(x, y, z + size),           // 4: Bottom-left-back
			new Vector3(x + size, y, z + size),  // 5: Bottom-right-back
			new Vector3(x, y + size, z + size),  // 6: Top-left-back
			new Vector3(x + size, y + size, z + size)   // 7: Top-right-back
		};
		if(index == 0)// top face of breaked block, add bottom face for top block
		{
			chunkInstance.chunkTriangles[position].Add((new Vector3[] { _vertices[0], _vertices[4], _vertices[5], _vertices[1] }, chunk.Face.Bottom, type));
		}
		if(index == 1)// bottom face of breaked block, add top face for bottom block
		{
			chunkInstance.chunkTriangles[position].Add((new Vector3[] { _vertices[2], _vertices[3], _vertices[7], _vertices[6] }, chunk.Face.Top, type));
		}
		if(index == 2)// left face of breaked block, add right face for left block
		{
			chunkInstance.chunkTriangles[position].Add((new Vector3[] { _vertices[3], _vertices[1], _vertices[5], _vertices[7]  }, chunk.Face.Right, type));
		}
		if(index == 3)// right face of breaked block, add left face for right block
		{
			chunkInstance.chunkTriangles[position].Add((new Vector3[] {_vertices[6], _vertices[4], _vertices[0], _vertices[2]  }, chunk.Face.Left, type));
		}
		if(index == 4)// front face of breaked block, add back face for front block
		{
			chunkInstance.chunkTriangles[position].Add((new Vector3[] { _vertices[2], _vertices[0], _vertices[1], _vertices[3] }, chunk.Face.Back, type));
		}
		if(index == 5)// back face of breaked block, add front face for back block
		{
			chunkInstance.chunkTriangles[position].Add((new Vector3[] { _vertices[7], _vertices[5], _vertices[4], _vertices[6] }, chunk.Face.Front, type));
		}

	}
	public void RemoveFace(Vector3 position, int index)
	{
		float x = position.X, y = position.Y, z = position.Z;
		Vector3[] _vertices = new Vector3[]
		{
			new Vector3(x, y, z),                   // 0: Bottom-left-front
			new Vector3(x + 1, y, z),           // 1: Bottom-right-front
			new Vector3(x, y + 1, z),           // 2: Top-left-front
			new Vector3(x + 1, y + 1, z),  // 3: Top-right-front
			new Vector3(x, y, z + 1),           // 4: Bottom-left-back
			new Vector3(x + 1, y, z + 1),  // 5: Bottom-right-back
			new Vector3(x, y + 1, z + 1),  // 6: Top-left-back
			new Vector3(x + 1, y + 1, z + 1)   // 7: Top-right-back
		};
		if(index == 0) // top face of added block, must remove bottom face of top block
		{
			List<(Vector3 [], chunk.Face, chunk.VoxelTexture) > list = chunkInstance.chunkTriangles[position];
			foreach((Vector3[], chunk.Face, chunk.VoxelTexture) v in list)
			{
				if(v.Item1[0] ==_vertices[0] && v.Item1[1] == _vertices[4] && v.Item1[2] == _vertices[5] && v.Item1[3] == _vertices[1])
				{
					list.Remove(v);
					break;
				}
			}
		}
		if(index == 1) // bottom face of added block, must remove top face of bottom block
		{
			List<(Vector3 [], chunk.Face, chunk.VoxelTexture)> list = chunkInstance.chunkTriangles[position];
			foreach((Vector3 [], chunk.Face, chunk.VoxelTexture) v in list)
			{
				if(v.Item1[0] ==_vertices[2] && v.Item1[1] == _vertices[3] && v.Item1[2] == _vertices[7] && v.Item1[3] == _vertices[6])
				{
					list.Remove(v);
					break;
				}
			}
		}
		if(index == 2) // left face of added block, must remove right face of left block
		{
			List<(Vector3 [], chunk.Face, chunk.VoxelTexture)> list = chunkInstance.chunkTriangles[position];
			foreach((Vector3 [], chunk.Face, chunk.VoxelTexture) v in list)
			{
				if(v.Item1[0] ==_vertices[3] && v.Item1[1] == _vertices[1] && v.Item1[2] == _vertices[5] && v.Item1[3] == _vertices[7])
				{
					list.Remove(v);
					break;
				}
			}
		}
		if(index == 3) // right face of added block, must remove left face of right block
		{
			List<(Vector3 [], chunk.Face, chunk.VoxelTexture)> list = chunkInstance.chunkTriangles[position];
			foreach((Vector3 [], chunk.Face, chunk.VoxelTexture) v in list)
			{
				if(v.Item1[0] ==_vertices[6] && v.Item1[1] == _vertices[4] && v.Item1[2] == _vertices[0] && v.Item1[3] == _vertices[2])
				{
					list.Remove(v);
					break;
				}
			}
		}
		if(index == 4)// front face of added block, remove back face for front block
		{
			List<(Vector3 [], chunk.Face, chunk.VoxelTexture)> list = chunkInstance.chunkTriangles[position];
			foreach((Vector3 [], chunk.Face, chunk.VoxelTexture) v in list)
			{
				if(v.Item1[0] ==_vertices[2] && v.Item1[1] == _vertices[0] && v.Item1[2] == _vertices[1] && v.Item1[3] == _vertices[3])
				{
					list.Remove(v);
					break;
				}
			}
		}
		if(index == 5)// back face of added block, remove front face for back block
		{
			List<(Vector3 [], chunk.Face, chunk.VoxelTexture)> list = chunkInstance.chunkTriangles[position];
			foreach((Vector3 [], chunk.Face, chunk.VoxelTexture) v in list)
			{
				if(v.Item1[0] ==_vertices[7] && v.Item1[1] == _vertices[5] && v.Item1[2] == _vertices[4] && v.Item1[3] == _vertices[6])
				{
					list.Remove(v);
					break;
				}
			}
		}
	}

	public Rect2 GetTileRegion(int tileX, int tileY, int tileSize, int atlasSize)
	{
		float uvX = (float)(tileX * tileSize) / atlasSize;
		float uvY = (float)(tileY * tileSize) / atlasSize;
		float uvWidth = (float)tileSize / atlasSize;
		float uvHeight = (float)tileSize / atlasSize;

		return new Rect2(uvX, uvY, uvWidth, uvHeight);
	}
	public Octree FindLeafAtPosition(Vector3 position)
	{
		if(children == null)
		{
			if(this.Contains(position, size))
			{
				return this;
			}
			return null;
		}
		foreach(var c in children)
		{
			if(c.Contains(position, c.size))
			{
				return c.FindLeafAtPosition(position);
			}
		}
	   return null;
	}
	private float GetVoxelOffset(float coordinate)
	{
		float remainder = coordinate - Mathf.Floor(coordinate);

		float large_offSet = 1, offset = 1, res;

		if(remainder == 0)
		{
			return size;
		}
		while(offset > remainder)
		{
			offset /= 2;
		}
		if(remainder - offset == 0)
		{
			return size > offset ? offset: size;
		}
		if(large_offSet - remainder < remainder - offset)
		{
			res = large_offSet - remainder;
		}
		else
		{

			res = remainder - offset > size ? size : remainder - offset;
		}
		return res;
	}
		public Octree FindLeafAtPosition_2(Vector3 position, Vector3 normal, float level)
	{
		if(children == null || level == size)
		{
			return this;
		}
		foreach(var c in children)
		{
			Vector3 s;

			if(normal.X > 0)
			{
				s = position - c.GetVoxelOffset(position.X)* normal;
			}
			else if(normal.Y > 0)
			{
				s = position - c.GetVoxelOffset(position.Y)* normal;
			}
			else if(normal.Z > 0)
			{
				s = position - c.GetVoxelOffset(position.Z)* normal;
			}
			else
			{
				s = position;
			}
			if(c.Contains(s, c.size))
			{
				// GD.Print("in HERE");
				return c.FindLeafAtPosition_2(position,normal, level);
			}
		}
	   return null;
	}

	public Octree FindNeighboursAtSameLevel(Vector3 position, float _size)
	{
		if(size == _size)
		{
			if(this.Contains(position, size))
			{
				return this;
			}
			return null;
		}
		if(children == null)
		{
			if(this.Contains(position, size))
			{
				return this;
			}
			return null;
		}
		foreach(var c in children)
		{
			if(c.Contains(position, c.size))
			{
				return c.FindNeighboursAtSameLevel(position,_size);
			}
		}
	   return null;
	}

	public bool ContainsSmallerNeighbours(Vector3 pos, float size, Vector3 position)
	{
		return pos.X >= position.X && pos.X < position.X + size &&
			   pos.Y >= position.Y && pos.Y < position.Y + size &&
			   pos.Z >= position.Z && pos.Z < position.Z + size;
	}
	public bool[] GetVisibleFaces(Octree root)
	{
		bool[] visibleFaces = new bool[6] { true, true, true, true, true, true };

		Vector3[] neighborOffsets = new Vector3[]
		{
			new Vector3(0, size, 0),  // Top
			new Vector3(0, -size, 0),  // Bottom
			new Vector3(-size, 0, 0), // Left
			new Vector3(size, 0, 0),  // Right
			new Vector3(0, 0, size),  // Front
			new Vector3(0, 0, -size), // Back
		};
		for (int i = 0; i < 6; i++)
		{
			Vector3 neighborPos = (Vector3)(this.position + neighborOffsets[i]);
			Octree temp = root.FindLeafAtPosition(neighborPos);

			if (temp != null)
			{

				if(temp.voxelType != VoxelType.Air && temp.size <= size)
				{
					visibleFaces[i] = false; // Neighbor exists, hide the face
				}
				
			}
		
		}

		return visibleFaces;
	}
	public void FreeOctree()
	{
		if (children != null)
		{
			foreach (var child in children)
			{
				if (child != null)
				{
					child.FreeOctree(); // Recursively free child nodes
				}
			}
			children = null; // Remove reference to children
		}
	}



}
