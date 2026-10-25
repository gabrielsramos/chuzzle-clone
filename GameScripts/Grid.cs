using ChuzzleClone.Utils;
using mgcore.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ChuzzleClone.GameScripts;

public struct GridMetadata
{
    public Coord Coordinates;
}

public class Grid : IDisposable
{
    private GraphicsDevice _graphicsDevice;
    
    private readonly (BallColor Color, Texture2D Texture)[] _ballTextures;
    
    private RenderEntity _root;
    private Ball?[,] _balls;
    private bool _logParticlePreparation;
    private long _particlePreparationStarted;
    private readonly List<Ball> _preparationTargets = new();
    // Cell occupancy is separate from the stable indices in _balls.
    private readonly (int Row, int Column)?[,] _ballIndicesByCell = new (int, int)?[rows, columns];
    private readonly Dictionary<(int Row, int Column), GridMetadata> _gridMetadata = new();
    private readonly HashSet<(int Row, int Column)> _explodingIndices = new();
    private enum GridState { Ready, Exploding, Falling, Refilling }
    private GridState _state;
    private readonly List<(Ball Ball, Vector2 Target)> _fallingBalls = new();
    private const float _fallSpeed = 600f;

    public IReadOnlyDictionary<(int Row, int Column), GridMetadata> BallMetadata => _gridMetadata;

    private MouseState _previousMouseState;
    private bool _isMousePressed;
    private bool _pressStartedOnBall;
    private Coord _pressedBallCoordinates;
    private DragAxis _dragAxis;
    private readonly List<(RenderEntity Source, RenderEntity Before, RenderEntity After)> _wrapCopies = new();

    private enum DragAxis { None, Horizontal, Vertical }
    
    private const int rows = 6;
    private const int columns = 6;
    private const int _spacing = 5;
    
    public int Width
    {
        get
        {
            var diameter = Ball.Width;

            return rows * diameter + (rows + 1) * _spacing;
        }
    }

    public int Height {
        get
        {
            var diameter = Ball.Width;

            return columns * diameter + (columns + 1) * _spacing;
        }
    }

    public Grid(GraphicsDevice graphicsDevice, (BallColor Color, Texture2D Texture)[] ballTextures)
    {
        _graphicsDevice = graphicsDevice;
        _ballTextures = ballTextures;
        _root = new RenderEntity();
        _root.DebugName = "Grid";
        _balls = new Ball?[rows, columns];
    }

    public void Initialize(Vector2 rootPosition)
    {
        ClearWrapCopies();
        DisposeBalls();
        _root.Texture?.Dispose();
        _gridMetadata.Clear();
        var text = DrawerHelper.CreateRectangleOutline(_graphicsDevice, Width, Height, 2, Color.Black);
        _root = new RenderEntity()
        {
            Texture = text,
            ClipRectangle = new Rectangle(-Width / 2, -Height / 2, Width, Height),
            Position = rootPosition
        };
        
        var topLeft = new Vector2(-Width / 2.0f, -Height / 2.0f);
        var circleRadius = Ball.Width / 2;
        
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                var x = topLeft.X + _spacing + circleRadius + (j * (circleRadius * 2 + _spacing));
                var y = topLeft.Y + _spacing + circleRadius + (i * (circleRadius * 2 + _spacing));

                var sprite = _ballTextures[Random.Shared.Next(_ballTextures.Length)];
                var ball = new Ball(sprite.Texture, sprite.Color, i, j);
                ball.Position = new Vector2(x, y);
                _balls[i, j] = ball;
                _ballIndicesByCell[i, j] = (i, j);
                _gridMetadata[(i, j)] = new GridMetadata
                {
                    Coordinates = new Coord { row = i, column = j }
                };
                
                _root.AddChild(ball.Entity);
            }
        }

        BeginMatchResolution();
    }

    public List<List<Coord>> FindMatches()
    {
        var matches = new List<List<Coord>>();
        ScanLines(rows, columns, horizontal: true, matches);
        ScanLines(columns, rows, horizontal: false, matches);
        return matches;
    }

    private void ScanLines(int lineCount, int lineLength, bool horizontal, List<List<Coord>> matches)
    {
        for (var line = 0; line < lineCount; line++)
        {
            var start = 0;
            while (start < lineLength)
            {
                var row = horizontal ? line : start;
                var column = horizontal ? start : line;
                var color = GetColor(row, column);
                var end = start + 1;
                while (end < lineLength)
                {
                    row = horizontal ? line : end;
                    column = horizontal ? end : line;
                    if (GetColor(row, column) != color)
                        break;
                    end++;
                }

                if (color.HasValue && end - start >= 3)
                {
                    var match = new List<Coord>();
                    for (var position = start; position < end; position++)
                        match.Add(new Coord
                        {
                            row = horizontal ? line : position,
                            column = horizontal ? position : line
                        });
                    matches.Add(match);
                }

                start = end;
            }
        }
    }

    private Ball? GetBallAt(int row, int column)
    {
        var index = _ballIndicesByCell[row, column];
        return index is { } occupied ? _balls[occupied.Row, occupied.Column] : null;
    }

    private BallColor? GetColor(int row, int column) => GetBallAt(row, column)?.BallColor;

    private void BeginMatchResolution()
    {
        var matches = FindMatches();
        LogMatches(matches);
        if (matches.Count == 0)
        {
            _state = GridState.Ready;
            return;
        }

        var matchedCells = new HashSet<(int Row, int Column)>();
        foreach (var match in matches)
            foreach (var coord in match)
                matchedCells.Add((coord.row, coord.column));

        var targets = new List<Ball>();
        foreach (var cell in matchedCells)
            if (GetBallAt(cell.Row, cell.Column) is { } ball)
                targets.Add(ball);
        ExplodeBalls(targets);
    }

    private void LogMatches(List<List<Coord>> matches)
    {
        if (matches.Count == 0)
        {
            Console.WriteLine("[Matches] No matches found.");
            return;
        }

        Console.WriteLine($"[Matches] Found {matches.Count} matches:");
        foreach (var match in matches)
            Console.WriteLine("[Matches] " + string.Join(", ",
                match.Select(coord => $"({coord.row}, {coord.column})")));
    }

    public void ExplodeAllBalls() => ExplodeBalls(_balls.OfType<Ball>());

    public void ExplodeMiddleRow() => ExplodeBalls(Enumerable.Range(0, columns).Select(column =>
    {
        return GetBallAt(rows / 2, column);
    }).OfType<Ball>());

    public void ExplodeMiddleColumn() => ExplodeBalls(Enumerable.Range(0, rows).Select(row =>
    {
        return GetBallAt(row, columns / 2);
    }).OfType<Ball>());

    private void ExplodeBalls(IEnumerable<Ball> balls)
    {
        if (_state != GridState.Ready)
            return;
        // Finish an active drag before resolving current cell occupants.
        SnapDraggedLine();
        _particlePreparationStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        _logParticlePreparation = true;
        // Remove wrap sprites so an in-progress drag cannot leave intact copies visible.
        ClearWrapCopies();
        _pressStartedOnBall = false;
        _dragAxis = DragAxis.None;
        _preparationTargets.Clear();
        _preparationTargets.AddRange(balls.Distinct());
        _explodingIndices.Clear();
        foreach (var ball in _preparationTargets)
        {
            var coord = ball.GridCoordinates;
            if (_ballIndicesByCell[coord.row, coord.column] is { } index)
                _explodingIndices.Add(index);
            ball.StartAnimation(BallAnimations.Exploding);
        }
        if (_explodingIndices.Count > 0)
            _state = GridState.Exploding;
    }

    private void UpdateExplosions()
    {
        // Remove only after UpdateBalls has finished iterating the board.
        foreach (var index in _explodingIndices.ToArray())
        {
            var ball = _balls[index.Row, index.Column];
            if (ball is not null && ball.CurrentAnimation is not null)
                continue;
            RemoveBall(index);
            _explodingIndices.Remove(index);
        }

        if (_explodingIndices.Count == 0)
        {
            _preparationTargets.Clear();
            _logParticlePreparation = false;
            BeginFalling();
        }
    }

    private void BeginFalling()
    {
        _fallingBalls.Clear();
        for (var column = 0; column < columns; column++)
        {
            var destinationRow = rows - 1;
            for (var row = rows - 1; row >= 0; row--)
            {
                if (_ballIndicesByCell[row, column] is not { } index)
                    continue;

                if (destinationRow != row)
                {
                    _ballIndicesByCell[row, column] = null;
                    AssignBallToCell(index, destinationRow, column);
                    var ball = _balls[index.Row, index.Column]!;
                    _fallingBalls.Add((ball, GetCellPosition(destinationRow, column)));
                }
                destinationRow--;
            }
        }
        _state = GridState.Falling;
    }

    private void AssignBallToCell((int Row, int Column) index, int row, int column)
    {
        _ballIndicesByCell[row, column] = index;
        _balls[index.Row, index.Column]!.SetCoords(row, column);
        _gridMetadata[index] = new GridMetadata
        {
            Coordinates = new Coord { row = row, column = column }
        };
    }

    private void UpdateFalling(GameTime gameTime)
    {
        if (UpdateFallingBalls(gameTime))
            BeginRefilling();
    }

    private void BeginRefilling()
    {
        _fallingBalls.Clear();
        var vacantIndices = new Queue<(int Row, int Column)>();
        for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
                if (_balls[row, column] is null)
                    vacantIndices.Enqueue((row, column));

        for (var column = 0; column < columns; column++)
        {
            var emptyRows = 0;
            while (emptyRows < rows && _ballIndicesByCell[emptyRows, column] is null)
                emptyRows++;

            for (var row = 0; row < emptyRows; row++)
            {
                var index = vacantIndices.Dequeue();
                var sprite = _ballTextures[Random.Shared.Next(_ballTextures.Length)];
                var ball = new Ball(sprite.Texture, sprite.Color, row, column);
                _balls[index.Row, index.Column] = ball;
                AssignBallToCell(index, row, column);
                ball.Position = GetCellPosition(row - emptyRows, column);
                _root.AddChild(ball.Entity);
                _fallingBalls.Add((ball, GetCellPosition(row, column)));
            }
        }
        _state = GridState.Refilling;
    }

    private void UpdateRefilling(GameTime gameTime)
    {
        if (UpdateFallingBalls(gameTime))
        {
            _state = GridState.Ready;
            // Consume any press held through the animation before accepting a new drag.
            _isMousePressed = true;
            _pressStartedOnBall = false;
            _dragAxis = DragAxis.None;

            BeginMatchResolution();
        }
    }

    private bool UpdateFallingBalls(GameTime gameTime)
    {
        var distance = _fallSpeed * Math.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
        for (var i = _fallingBalls.Count - 1; i >= 0; i--)
        {
            var (ball, target) = _fallingBalls[i];
            var position = ball.Position;
            position.Y = Math.Min(position.Y + distance, target.Y);
            ball.Position = position;
            if (position.Y >= target.Y)
            {
                ball.Position = target;
                _fallingBalls.RemoveAt(i);
            }
        }
        return _fallingBalls.Count == 0;
    }

    private void RemoveBall((int Row, int Column) index)
    {
        var ball = _balls[index.Row, index.Column];
        if (ball is not null)
        {
            var coord = ball.GridCoordinates;
            _ballIndicesByCell[coord.row, coord.column] = null;
            _preparationTargets.Remove(ball);
            ball.Dispose();
        }
        _balls[index.Row, index.Column] = null;
        _gridMetadata.Remove(index);
    }

    public void Reset()
    {
        ClearWrapCopies();
        _isMousePressed = false;
        _pressStartedOnBall = false;
        _pressedBallCoordinates = default;
        _previousMouseState = default;
        _dragAxis = DragAxis.None;
        _logParticlePreparation = false;
        _preparationTargets.Clear();
        _explodingIndices.Clear();
        _fallingBalls.Clear();
        _state = GridState.Ready;
        _gridMetadata.Clear();

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var ball = _balls[row, column];
                if (ball is null)
                {
                    var sprite = _ballTextures[Random.Shared.Next(_ballTextures.Length)];
                    ball = new Ball(sprite.Texture, sprite.Color, row, column);
                    _balls[row, column] = ball;
                    _root.AddChild(ball.Entity);
                }
                ball.StartAnimation(BallAnimations.Idle);
                ball.Position = GetCellPosition(row, column);
                ball.SetCoords(row, column);
                _ballIndicesByCell[row, column] = (row, column);
                _gridMetadata[(row, column)] = new GridMetadata
                {
                    Coordinates = new Coord { row = row, column = column }
                };
            }
        }
    }

    public void Update(GameTime gameTime)
    {
        UpdateBalls(gameTime);

        switch (_state)
        {
            case GridState.Ready:
                UpdateDragging();
                break;
            case GridState.Exploding:
                UpdateExplosions();
                break;
            case GridState.Falling:
                UpdateFalling(gameTime);
                break;
            case GridState.Refilling:
                UpdateRefilling(gameTime);
                break;
        }
    }

    private void UpdateDragging()
    {
        var mouseState = Mouse.GetState();

        if (mouseState.LeftButton == ButtonState.Pressed)
        {
            if (!_isMousePressed)
            {
                (_pressStartedOnBall, _pressedBallCoordinates) = IsInsideBall(mouseState.Position.ToVector2());
                _dragAxis = DragAxis.None;
            }
            else if (_pressStartedOnBall)
            {
                DragBalls(mouseState);
            }

            _isMousePressed = true;
        }
        else
        {
            if (_isMousePressed && _pressStartedOnBall)
            {
                // Include any movement since the last pressed frame.
                DragBalls(mouseState);
                var wasDragging = _dragAxis != DragAxis.None;
                SnapDraggedLine();
                if (wasDragging)
                    BeginMatchResolution();
            }

            _isMousePressed = false;
            _pressStartedOnBall = false;
            _dragAxis = DragAxis.None;
        }

        _previousMouseState = mouseState;
        UpdateWrapCopies();
    }

    private void UpdateBalls(GameTime gameTime)
    {
        var stillPreparing = false;
        double preparationTotal = 0, preparationMax = 0, attachmentTotal = 0;
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                _balls[i, j]?.Update(gameTime);
            }
        }
        if (_logParticlePreparation)
        {
            foreach (var ball in _preparationTargets)
            {
                stillPreparing |= ball.CurrentExplosionPhase == ExplosionPhase.CreateParticles;
                preparationTotal += ball.ParticlePreparationMilliseconds;
                preparationMax = Math.Max(preparationMax, ball.ParticlePreparationMilliseconds);
                attachmentTotal += ball.ParticleAttachmentMilliseconds;
            }
        }
        if (_logParticlePreparation && !stillPreparing)
        {
            _logParticlePreparation = false;
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(_particlePreparationStarted).TotalMilliseconds;
            Console.WriteLine($"[Particles] Grid preparation finished in {elapsed:0.##} ms wall time; " +
                $"worker time total {preparationTotal:0.##} ms, slowest ball {preparationMax:0.##} ms; " +
                $"game-thread attachment total {attachmentTotal:0.##} ms.");
        }
    }

    private void DragBalls(MouseState mouseState)
    {
        var delta = mouseState.Position.ToVector2() - _previousMouseState.Position.ToVector2();
        if (delta.LengthSquared() == 0)
            return;

        if (_dragAxis == DragAxis.None)
        {
            _dragAxis = Math.Abs(delta.X) > Math.Abs(delta.Y) ? DragAxis.Horizontal : DragAxis.Vertical;
            CreateWrapCopies();
        }

        if (_dragAxis == DragAxis.Horizontal)
        {
            MoveBallsHoriontally(_pressedBallCoordinates, delta.X);
        }
        else
        {
            MoveBallsVertically(_pressedBallCoordinates, delta.Y);
        }
    }

    private void SnapDraggedLine()
    {
        if (_dragAxis == DragAxis.None)
            return;

        var horizontal = _dragAxis == DragAxis.Horizontal;
        var count = horizontal ? columns : rows;
        var cellSize = (horizontal ? Ball.Width : Ball.Height) + _spacing;
        // Use the first cell's occupant as the anchor for the whole line.
        // Its current position determines the nearest cell, even if movement
        // was applied outside DragBalls.
        var anchorIndex = horizontal
            ? _ballIndicesByCell[_pressedBallCoordinates.row, 0]
            : _ballIndicesByCell[0, _pressedBallCoordinates.column];
        var anchor = anchorIndex!.Value;
        var anchorPosition = _balls[anchor.Row, anchor.Column]!.Position;
        var origin = GetCellPosition(0, 0);
        var offset = horizontal ? anchorPosition.X - origin.X : anchorPosition.Y - origin.Y;
        var shift = (int)MathF.Round(offset / cellSize, MidpointRounding.AwayFromZero);
        var line = new (int Row, int Column)[count];

        // Snapshot only this line so rotating its occupants cannot overwrite them.
        for (int i = 0; i < count; i++)
            line[i] = horizontal
                ? _ballIndicesByCell[_pressedBallCoordinates.row, i]!.Value
                : _ballIndicesByCell[i, _pressedBallCoordinates.column]!.Value;

        for (int i = 0; i < count; i++)
        {
            var destination = ((i + shift) % count + count) % count;
            var row = horizontal ? _pressedBallCoordinates.row : destination;
            var column = horizontal ? destination : _pressedBallCoordinates.column;
            var index = line[i];
            var ball = _balls[index.Row, index.Column]!;
            ball.Position = GetCellPosition(row, column);
            _ballIndicesByCell[row, column] = index;

            var coordinates = _gridMetadata[index].Coordinates;
            if (coordinates.row != row || coordinates.column != column)
            {
                _gridMetadata[index] = new GridMetadata
                {
                    Coordinates = new Coord { row = row, column = column }
                };
                ball.SetCoords(row, column);
            }
        }

        ClearWrapCopies();
    }

    private void CreateWrapCopies()
    {
        var horizontal = _dragAxis == DragAxis.Horizontal;
        var count = horizontal ? columns : rows;
        for (int i = 0; i < count; i++)
        {
            var index = horizontal
                ? _ballIndicesByCell[_pressedBallCoordinates.row, i]
                : _ballIndicesByCell[i, _pressedBallCoordinates.column];
            var occupied = index!.Value;
            var source = _balls[occupied.Row, occupied.Column]!.Entity;
            var before = new RenderEntity { DebugName = "Wrap copy before" };
            var after = new RenderEntity { DebugName = "Wrap copy after" };
            _root.AddChild(before);
            _root.AddChild(after);
            _wrapCopies.Add((source, before, after));
        }
    }

    private void UpdateWrapCopies()
    {
        var offset = _dragAxis == DragAxis.Horizontal
            ? new Vector2(columns * (Ball.Width + _spacing), 0)
            : new Vector2(0, rows * (Ball.Height + _spacing));

        foreach (var (source, before, after) in _wrapCopies)
        {
            SynchronizeWrapCopy(source, before, source.Position - offset);
            SynchronizeWrapCopy(source, after, source.Position + offset);
        }
    }

    private static void SynchronizeWrapCopy(RenderEntity source, RenderEntity copy, Vector2 position)
    {
        copy.Position = position;
        copy.Texture = source.Texture;
        copy.SourceRectangle = source.SourceRectangle;
        copy.Color = source.Color;
        copy.Origin = source.Origin;
        copy.Effects = source.Effects;
        copy.LayerDepth = source.LayerDepth;
    }

    private void ClearWrapCopies()
    {
        foreach (var (_, before, after) in _wrapCopies)
        {
            _root.RemoveChild(before);
            _root.RemoveChild(after);
        }
        _wrapCopies.Clear();
    }

    private static float GetWrappedDelta(float anchorOffset, float delta, float cycle)
    {
        // Translate the entire line by the same amount, discarding full cycles.
        // The originals and copies one cycle away then cover arbitrarily long drags.
        return (anchorOffset + delta) % cycle - anchorOffset;
    }

    private Vector2 GetCellPosition(int row, int column)
    {
        return new Vector2(
            -Width / 2f + _spacing + Ball.Width / 2f + column * (Ball.Width + _spacing),
            -Height / 2f + _spacing + Ball.Height / 2f + row * (Ball.Height + _spacing));
    }

    private void MoveBallsHoriontally(Coord coordinates, float delta)
    {
        var row = coordinates.row;
        var anchor = _ballIndicesByCell[row, 0]!.Value;
        delta = GetWrappedDelta(
            _balls[anchor.Row, anchor.Column]!.Position.X - GetCellPosition(row, 0).X,
            delta, columns * (Ball.Width + _spacing));

        for (int j = 0; j < columns; j++)
        {
            var index = _ballIndicesByCell[row, j]!.Value;
            var ent = _balls[index.Row, index.Column]!.Entity;
            var position = ent.Position;
            position.X += delta;
            ent.Position = position;
        }
    }
    
    private void MoveBallsVertically(Coord coordinates, float delta)
    {
        var column = coordinates.column;
        var anchor = _ballIndicesByCell[0, column]!.Value;
        delta = GetWrappedDelta(
            _balls[anchor.Row, anchor.Column]!.Position.Y - GetCellPosition(0, column).Y,
            delta, rows * (Ball.Height + _spacing));

        for (int i = 0; i < rows; i++)
        {
            var index = _ballIndicesByCell[i, column]!.Value;
            var ent = _balls[index.Row, index.Column]!.Entity;
            var position = ent.Position;
            position.Y += delta;
            ent.Position = position;
        }
    }

    private (bool, Coord) IsInsideBall(Vector2 point)
    {
        var radius = Ball.Width / 2f;
        var radiusSquared = radius * radius;
        var coord = new Coord();

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                var ball = _balls[i, j];
                if (ball is null)
                    continue;
                var ballCenter = _root.Position + ball.Position;

                if ((point - ballCenter).LengthSquared() <= radiusSquared)
                {
                    coord = _gridMetadata[(i, j)].Coordinates;
                    return (true, coord);
                }
            }
        }

        return (false, coord);
    }
    

    public RenderEntity GetGrid()
    {
        return _root;
    }

    private void DisposeBalls()
    {
        foreach (var ball in _balls)
            ball?.Dispose();
        Array.Clear(_balls);
        Array.Clear(_ballIndicesByCell);
        _gridMetadata.Clear();
        _explodingIndices.Clear();
        _fallingBalls.Clear();
        _preparationTargets.Clear();
        _logParticlePreparation = false;
        _state = GridState.Ready;
    }

    public void Dispose()
    {
        ClearWrapCopies();
        DisposeBalls();
        _root.Texture?.Dispose();
        _root.Texture = null;
    }
}
