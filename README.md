# Octree Game
Before staring, I want to mention that **this project was done under the supervision of Prof. Roi Poranne, as part of the requirements for a BSc in computer science at the University of Haifa**

![INTRO-IMAGE](images/INTRO-IMAGE.png)
## Introduction
In this project I created a Minecraft clone game. But with few modifications. Before addressing these modifications, I will explain how the world is created.<br /><br />
Key features:
- Infinite terrain generation.
- Place block/ place multiple blocks at once.
- Break block/ break and spliting blocks on different levels.
<br />

Feel free to check the [playlist](https://www.youtube.com/watch?v=Swm-AQIH1bw&list=PLrF1CJnnYlq_D1w6doglLAetkI2DD9JLo) on my Youtube channel

## Structure
### Creation
The world consists from chunks, each chunk is an octree. The world generatation is done using multithreading to utilise all available CPU core.<br />
Each chunk is being handled on different thread. When all the chunks are fully generated, we add mesh to them. Each chunk mesh generatation is occuring on different thread.<br /> When generation of the mesh is completed , the player can start play.

### Data Structure
As we mentioned earlier, the main data structure is an octree, and a list of chunks. The creation of the world start from one big node with size of the chunk. I implemented a Divide function, which divide this node according to the desired LOD which is requested when generating the world. after reaching the specific LOD, according to a 3d noise function, the state of the voxel is determined, air or solid.
### Mesh generation
To generate mesh for the octree, the only nodes that we must consider or look at is the leaf nodes. Instead of doing a recursive approach we did an iterative approach using Queue. When the queue has all the leaf nodes (their state is solid) it start generating triangles for the voxels that must be rendered. I rendered only the visible faces of the voxels. For each voxel, I checked it's neighbour, if one of his neighbour is solid, we dont render that face which with it a boundry, if the neighbour is Air then we render this face and etc. For each chunk we save for each position, the triangles that must be rendered.

## Features

### Build
- When building a block in a chunk, we dont need to create new nodes, the node is already created, we only need to traverse on the path which lead us to it. Then, we change the node type to Solid, then we calculate the triangles that needed to be generated, we must notice that if we are adding the block on the top of another block, we must remove the top face of the bottom block, the same for other direction, (the other six neighbours). And then we generate the triangles of the block that we want to add. <br />This approach effienctlly add and remove triangles, without the need to generate the triangles of all the chunk once again. <br />

- We added a UI for the player, which he can decide how many blocks he want to build/place in the chunk. I designed a way which he can choose how many block he want to add across multiple axis, X,Y,Z and then use the The build function to place those block in the world. Sometimes, adding multiple block at the same time can happen on multiple neighbouring chunks. In that case, we check the chunk position of each node we place if it variey from the current chunk we will call the place function from the other chunk and update the octree and triangles like we discussed before.<br />
![Build](images/BUILD-UI.png)

### Remove Block

- When removing blocks, we must take care of similar cases like when we placed blocks, when we delete a block, we must adjust the neighbouring blocks as well. <br /> If the broken block is on the left of other block, we must render the left face of the neighboring block. The same when it on top of some block, then we must render the top face of the bottom block.

- When using removing and spliting feature. The node is divided to 8 children, the octant at the hit position will be Air, the others will be solid. Now we generate the triangles of new octant as the same way we did before. But there is a problem, what if we broke now other octant at lowest level, it will also be divided to 8 octants and 7 children will only be rendered. But what abut the boundry and it's neighbour at a higher level. in the case the face of the neighbour will not be rendered. So, we must take care of this case. I achieved that by checking if the neighbour size is bigger that the current node, if yes then i generate triangles of the face of the larger node. This approach can be applied also for every level in the octree. To make the removing and spliting block more interesting, I added as my supervisor recommended, a way to break multiple blocks at the same lower level of the octree.<br /> By pressing G in game, the player can change the mode for breaking and spliting, the default size is 1. <br />
If the player pressed 2 the size of breaking a block will be 0.5, and he can remove multiple blocks of size 0.5, If he pressed 3 the size will be 0.25 and etc. till it reachs the size of 0.0625.<br />
![Break](images/BREAK-SPLIT.png)



## How To Play

- Movment of player:
    1. W - UP
    2. S - DOWN
    3. A - LEFT
    4. D - RIGHT
    5. Space - JUMP
- Features:
    1. Left mouse click - break block
    2. Right mouse click - build block
    3. G - change modes between break/ break and split
    4. Shift - To display build UI.
To switch between block types, you can enter the number at the right of each type. <br />
To Break blocks of muliples level, you can use 1,2,3,4,5. as the number gets higher the level gets deeper/lower.


## Future Work

Instead of using regular Octrees, we can use Sparse Voxel Octree, i already have a demo which partially working. This specfic type of structure is really effiecent and Memory freindly. To maintain a such data structure, I need to design much complex algorithms for mesh generation. One bigger advantage of using this DS is it reduces memory which is needed to build each chunk, becasue this DS splits nodes when it is needed to split. Fo example for a large area which is all solid instead of represnting it using multiple nodes we can represent it with one big solid node. <br /> Disadvantage is that searching for a block take at most O(logn) while if we used Grid it will take only O(1). But using grid will need larger memory.

<br />
Maybe in the future i will use a more of a Hybrid approach of handling memory.


