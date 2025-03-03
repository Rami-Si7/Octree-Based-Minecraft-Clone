using Godot;
using System;

public partial class player : CharacterBody3D
{	public const float Speed = 5.0f;       // Horizontal movement speed
	public const float FlySpeed = 10.0f;   // Vertical movement speed
	public const float LookSensitivity = 0.1f; // Mouse look sensitivity
	private const float Gravity = 9.8f;
	private const float JumpForce = 5.0f;

	private Vector3 _velocity = Vector3.Zero;  // Player's velocity
	private Vector2 _lookDelta = Vector2.Zero; // For mouse movement tracking

	[Export]public RayCast3D rayCast3D;
	[Export] private Camera3D camera3D;
	[Export] public MeshInstance3D highlight;
	[Export] private Node3D _axe;
	[Export] private AnimationPlayer _axeAnimation;
	[Export] private AudioStreamPlayer3D jumpAudio;
	[Export] private AudioStreamPlayer3D buildAudio;
	[Export] private AudioStreamPlayer3D breakAudio;
	public Vector3 highlightScale = new Vector3(1,1,1);
	private Transform3D playerTransform;
	public bool canMove = false;
	[Export]private Node2D accuracy;
	MeshInstance2D mesh_1;
	MeshInstance2D mesh_2;
	CanvasItemMaterial canvasItemMaterial;
	chunk.VoxelTexture voxelTexture = chunk.VoxelTexture.Dirt;

	private int scale = 1;

	public bool adjustingSliderX = false;
	public bool adjustingSliderY = false;
	public bool adjustingSliderZ = false;
	
	public void _on_x_drag_ended(bool value)
	{
		GD.Print("2");
		adjustingSliderX = false;
	}

	public void _on_x_drag_started()
	{
		GD.Print("1");
		adjustingSliderX = true;
	}
	public void _on_y_drag_ended(bool value)
	{
		adjustingSliderY = false;
	}

	public void _on_y_drag_started()
	{
		adjustingSliderY = true;
	}
	public void _on_z_drag_ended(bool value)
	{
		adjustingSliderZ = false;
	}

	public void _on_z_drag_started()
	{
		adjustingSliderZ = true;
	}
	public void _on_animation_player_animation_finished(string animeName)
	{
		_axeAnimation.Play("blank");
	}


	public override void _Ready()
	{
		// Capture the mouse for first-person controls
		Input.MouseMode = Input.MouseModeEnum.Captured;
		playerTransform = GlobalTransform;
		accuracy.GlobalPosition = DisplayServer.WindowGetSize()/2;
		mesh_1 = (MeshInstance2D)accuracy.GetChild(0);
		mesh_2 = (MeshInstance2D)accuracy.GetChild(1);
		canvasItemMaterial = new CanvasItemMaterial();
		_axeAnimation.Play("blank");
	}

	public override void _Input(InputEvent @event)
	{
		// Handle mouse motion for looking around
		if (@event is InputEventMouseMotion mouseMotion)
		{
			_lookDelta = mouseMotion.Relative;
		}

		// Toggle mouse capture with Escape
		if (@event.IsActionPressed("ui_cancel"))
		{
			Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
				? Input.MouseModeEnum.Visible
				: Input.MouseModeEnum.Captured;
		}
	}
	public override void _PhysicsProcess(double delta)
	{
		World.instance.xAxisLabel.Text = ((int)World.instance.xSlider.Value).ToString();
		World.instance.yAxisLabel.Text = ((int)World.instance.ySlider.Value).ToString();
		World.instance.zAxisLabel.Text = ((int)World.instance.zSlider.Value).ToString();
		if(!canMove)
		{
			return;
		}
		HandleMovement(delta);
		HandleLook();
		if(Input.IsActionJustPressed("swap"))
		{
			Global.octreeBreak = !Global.octreeBreak;
			
		}
		if(Input.IsActionJustPressed("Wood"))
		{
			voxelTexture = HandleVoxelType(1);
			Global.level = 1f;
		}
		if(Input.IsActionJustPressed("Brick"))
		{
			voxelTexture = HandleVoxelType(2);
			Global.level = 0.5f;
		}
		if(Input.IsActionJustPressed("Dirt"))
		{
			voxelTexture = HandleVoxelType(3);
			Global.level = 0.25f;

		}
		if(Input.IsActionJustPressed("Grass"))
		{
			voxelTexture = HandleVoxelType(4);
			Global.level = 0.125f;
		}
		if(Input.IsActionJustPressed("RedBrick"))
		{
			voxelTexture = HandleVoxelType(5);
			Global.level = 0.0625f;
		}
		if(Input.IsActionJustPressed("BuildingWood"))
		{
			voxelTexture = HandleVoxelType(6);
		}
		if(Input.IsActionJustPressed("ScaleUp"))
		{
			scale += 1;
		}
		if(Input.IsActionJustPressed("ScaleDown"))
		{
			if(scale > 0) scale -= 1;
		}
		if(Input.IsActionJustPressed("ViewBuild"))
		{
			highlight.Visible = !highlight.Visible;
			World.instance.vBoxContainer.Visible = ! World.instance.vBoxContainer.Visible; 
		}
		
		bool sliderRes = adjustingSliderX || adjustingSliderY || adjustingSliderZ;
		if(rayCast3D.IsColliding() && rayCast3D.GetCollider() is chunk chunk && !sliderRes)
		{
			int xSlider = (int)World.instance.xSlider.Value;
			int ySlider = (int)World.instance.ySlider.Value;
			int zSlider = (int)World.instance.zSlider.Value;
			SetHighlightScale( xSlider + 0.01f, ySlider + 0.01f, zSlider + 0.01f);

			accuracy.GlobalPosition = camera3D.UnprojectPosition(rayCast3D.GetCollisionPoint());
			mesh_1.Modulate = Colors.White;
			mesh_2.Modulate = Colors.White;
			if(Input.IsActionJustPressed("break"))
			{
				_axeAnimation.Play("Swing");
				
				var exactPosition = rayCast3D.GetCollisionPoint();
				var blockPosition = rayCast3D.GetCollisionPoint() - 0.5f * rayCast3D.GetCollisionNormal();
				GD.Print("exactPosition of breaking block", exactPosition, "normal: ",rayCast3D.GetCollisionNormal());
				chunk chunkNode = World.instance.GetChunkAt(blockPosition);
				if(chunkNode != null)
				{
					GD.Print("chunkNode", chunkNode.GlobalTransform.Origin);
					chunkNode.BreakBlock(exactPosition - chunkNode.GlobalTransform.Origin, rayCast3D.GetCollisionNormal());
				}
				breakAudio.Play();
			}
			if(Input.IsActionJustPressed("place"))
			{
				var exactPosition = rayCast3D.GetCollisionPoint();
				var normal = rayCast3D.GetCollisionNormal();
				var blockPosition = rayCast3D.GetCollisionPoint() - 0.5f * normal;
				chunk chunkNode = World.instance.GetChunkAt(blockPosition + normal);
				if(chunkNode != null)
				{
					// Vector3 playerPosition = new Vector3(Mathf.FloorToInt(GlobalPosition.X), Mathf.FloorToInt(GlobalPosition.Y), Mathf.FloorToInt(GlobalPosition.Z));
					// if(!exactPosition.Equals(playerPosition))
					// {
						GD.Print($"chunkNode{chunkNode.GlobalTransform.Origin}, pos: {rayCast3D.GetCollisionPoint()}");
						// int xSlider = (int)World.instance.xSlider.Value;
						chunkNode.PlaceBlock(exactPosition, rayCast3D.GetCollisionNormal(), voxelTexture, xSlider, ySlider, zSlider);
						buildAudio.Play();
					// }
				}
			}
		}
		else
		{
			accuracy.GlobalPosition = DisplayServer.WindowGetSize()/2;
			mesh_1.Modulate = Colors.Red;
			mesh_2.Modulate = Colors.Red;
			SetHighlightScale(0,0,0);
		}
	}
	private chunk.VoxelTexture HandleVoxelType(int number)
	{
		if(number == 1)
		{
			return chunk.VoxelTexture.Wood;
		}
		else if(number == 2)
		{
			return chunk.VoxelTexture.Bricks;
		}
		else if(number == 3)
		{
			return chunk.VoxelTexture.Dirt;
		}
		else if(number == 4)
		{
			return chunk.VoxelTexture.Grass;
		}
		else if(number == 5)
		{
			return chunk.VoxelTexture.RedBricks;
		}
		else if(number == 6)
		{
			return chunk.VoxelTexture.BuildingWood;
		}
		else
		{
			return chunk.VoxelTexture.Dirt;
		}
	}
	private void SetHighlightScale(float xScale, float yScale, float zScale)
	{
		SetScale(xScale, yScale, zScale);
		highlight.Scale = highlightScale;
		Vector3 normal = rayCast3D.GetCollisionNormal();
		Vector3 vector = rayCast3D.GetCollisionPoint();
		if(normal.X < 0)
		{
			vector.Y = Mathf.FloorToInt(yScale) % 2 == 0 ? Mathf.FloorToInt(vector.Y): Mathf.FloorToInt(vector.Y) - 0.5f * normal.X;
			vector.Z = Mathf.FloorToInt(zScale) % 2 == 0 ? Mathf.FloorToInt(vector.Z): Mathf.FloorToInt(vector.Z) - 0.5f * normal.X;
			vector.X += (xScale/2f) * normal.X;
		}
		if(normal.X > 0)
		{
			vector.Y = Mathf.FloorToInt(yScale) % 2 == 0 ? Mathf.FloorToInt(vector.Y): Mathf.FloorToInt(vector.Y) + 0.5f * normal.X;
			vector.Z = Mathf.FloorToInt(zScale) % 2 == 0 ? Mathf.FloorToInt(vector.Z): Mathf.FloorToInt(vector.Z) + 0.5f * normal.X;

			vector.X += (xScale/2f) * normal.X;
		}
		if(normal.Y < 0)
		{
			vector.X = Mathf.FloorToInt(vector.X);
			vector.Z = Mathf.FloorToInt(vector.Z);
			vector.X -= (xScale/2f) * normal.Y;
			vector.Z -= (zScale/2f) * normal.Y;
			vector.Y -= (yScale/2f) * normal.Y;
		}
		if(normal.Y > 0)
		{
			vector.X = Mathf.FloorToInt(vector.X);
			vector.Z = Mathf.FloorToInt(vector.Z);
			vector.X += (xScale/2f) * normal.Y;
			vector.Z += (zScale/2f) * normal.Y;
			vector.Y += (yScale/2f) * normal.Y;
		}
		if(normal.Z < 0)
		{
			vector.Y = vector.Y = Mathf.FloorToInt(yScale) % 2 == 0 ? Mathf.FloorToInt(vector.Y): Mathf.FloorToInt(vector.Y) - 0.5f * normal.Z;
			vector.X = Mathf.FloorToInt(xScale) % 2 == 0 ? Mathf.FloorToInt(vector.X): Mathf.FloorToInt(vector.X) - 0.5f * normal.Z;
			vector.Z += (zScale/2f) * normal.Z;
		}
		if(normal.Z > 0)
		{
			vector.Y = Mathf.FloorToInt(yScale) % 2 == 0 ? Mathf.FloorToInt(vector.Y): Mathf.FloorToInt(vector.Y) + 0.5f * normal.Z;
			vector.X = Mathf.FloorToInt(xScale) % 2 == 0 ? Mathf.FloorToInt(vector.X): Mathf.FloorToInt(vector.X) + 0.5f * normal.Z;
			vector.Z += (zScale/2f) * normal.Z;
		}
		// vector.X = Mathf.FloorToInt(vector.X);
		highlight.GlobalPosition = vector;
		highlight.GlobalRotation = Vector3.Zero;
	}
	private void SetScale(float xScale, float yScale, float zScale)
	{
		highlightScale.X = xScale;
		highlightScale.Y = yScale;
		highlightScale.Z = zScale;
	}
	// private void HandleMovement(double delta)
	// {
	// 	// Capture player input for movement
	// 	Vector3 direction = Vector3.Zero;
	// 	Vector3 rotation = RotationDegrees;
	// 	rotation.Y -= _lookDelta.X * LookSensitivity;
	// 	rotation.X -= _lookDelta.Y * LookSensitivity;
	// 	RotationDegrees = rotation;
	// 	_lookDelta = Vector2.Zero;

	// 	// Horizontal movement (WASD or arrow keys)
	// 	if (Input.IsActionPressed("UP")) // Move forward
	// 		direction -= Transform.Basis.Z;
	// 	if (Input.IsActionPressed("DOWN")) // Move backward
	// 		direction += Transform.Basis.Z;
	// 	if (Input.IsActionPressed("LEFT")) // Move left
	// 		direction -= Transform.Basis.X;
	// 	if (Input.IsActionPressed("RIGHT")) // Move right
	// 		direction += Transform.Basis.X;

	// 	// Vertical movement (Space and Shift)
	// 	if (Input.IsActionPressed("JUMP")) // Ascend
	// 		direction += Vector3.Up;
	// 	// if (Input.IsActionPressed("move_down")) // Descend
	// 	// 	direction -= Vector3.Up;


	// 	// Normalize the direction vector to ensure consistent speed
	// 	direction = direction.Normalized();

	// 	// Apply movement speed
	// 	_velocity = direction * Speed;

	// 	// Move the player
	// 	Velocity = _velocity;
	// 	MoveAndSlide();
	// }
	private void HandleMovement(double delta)
{
	// Capture player input for movement
	Vector3 direction = Vector3.Zero;
	Vector3 rotation = RotationDegrees;
	rotation.Y -= _lookDelta.X * LookSensitivity;
	rotation.X -= _lookDelta.Y * LookSensitivity;
	RotationDegrees = rotation;
	_lookDelta = Vector2.Zero;

	// Horizontal movement (WASD or arrow keys)
	if (Input.IsActionPressed("UP")) // Move forward
		direction -= Transform.Basis.Z;
	if (Input.IsActionPressed("DOWN")) // Move backward
		direction += Transform.Basis.Z;
	if (Input.IsActionPressed("LEFT")) // Move left
		direction -= Transform.Basis.X;
	if (Input.IsActionPressed("RIGHT")) // Move right
		direction += Transform.Basis.X;

	// Normalize the direction vector to ensure consistent speed
	direction = direction.Normalized();

	// Apply movement speed
	_velocity.X = direction.X * Speed;
	_velocity.Z = direction.Z * Speed;

	// Apply gravity
	if (!IsOnFloor()) // Check if the player is in the air
	{
		_velocity.Y -= Gravity * (float)delta; // Apply gravity
	}
	else if (Input.IsActionPressed("JUMP")) // Jump only if on the ground
	{
		_velocity.Y = JumpForce;
		jumpAudio.Play();
	}

	// Move the player
	Velocity = _velocity;
	MoveAndSlide();
}

	private void HandleLook()
	{
		// Rotate the player left/right (yaw) based on mouse X movement
		RotateY(-Mathf.DegToRad(_lookDelta.X));

		// Rotate the camera up/down (pitch) based on mouse Y movement
		Node3D camera = GetNode<Node3D>("Camera3D");
		if (camera != null)
		{
			float rotationX = camera.RotationDegrees.X - _lookDelta.Y;
			rotationX = Mathf.Clamp(rotationX, -70.0f, 70.0f); // Prevent looking too far up or down
			camera.RotationDegrees = new Vector3(rotationX, camera.RotationDegrees.Y, camera.RotationDegrees.Z);
		}

		// Reset look delta
		_lookDelta = Vector2.Zero;
	}
}
