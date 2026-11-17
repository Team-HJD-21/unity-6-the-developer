using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>목표 속도·감쇠 보상·제동과 외부 충돌에 의한 밀림을 검사한다.</summary>
public sealed class EnemyForceMovementTests
{
    private Scene _scene;
    private PhysicsScene2D _physics;
    private Rigidbody2D _body;
    private EnemyMovement _movement;

    [SetUp]
    public void SetUp()
    {
        _scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        _physics = _scene.GetPhysicsScene2D();
        Assert.That(_physics.Equals(Physics2D.defaultPhysicsScene), Is.False);
        _body = CreateBody("Enemy", Vector2.zero);
        _body.linearDamping = 5f;
        _movement = _body.gameObject.AddComponent<EnemyMovement>();
        typeof(EnemyMovement).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_movement, null);
    }

    [TearDown]
    public void TearDown()
    {
        if (!_scene.IsValid())
            return;
        foreach (GameObject root in _scene.GetRootGameObjects())
            Object.DestroyImmediate(root);
        UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(_scene);
    }

    [Test]
    public void Move_AcceleratesThroughPhysicsAndCanChangeDirection()
    {
        _movement.Move(Vector2.right);
        Assert.That(_body.linearVelocity, Is.EqualTo(Vector2.zero));
        _physics.Simulate(Time.fixedDeltaTime);
        Assert.That(_body.linearVelocity.x, Is.GreaterThan(0f));

        for (int i = 0; i < 100; i++)
        {
            _movement.Move(Vector2.right);
            _physics.Simulate(Time.fixedDeltaTime);
        }
        Assert.That(_body.linearVelocity.x,
            Is.EqualTo(_movement.MoveSpeed).Within(0.01f));

        for (int i = 0; i < 100; i++)
        {
            _movement.Move(Vector2.left);
            _physics.Simulate(Time.fixedDeltaTime);
        }
        Assert.That(_body.linearVelocity.x, Is.LessThan(0f));
    }

    [TestCase(0f, 1f)]
    [TestCase(2.5f, 50f)]
    [TestCase(5f, 1000f)]
    public void Move_AccelerationAndTargetSpeedAreIndependentOfMassAndDamping(
        float damping, float mass)
    {
        _body.linearDamping = damping;
        _body.mass = mass;

        _movement.Move(Vector2.right);
        _physics.Simulate(Time.fixedDeltaTime);
        Assert.That(_body.linearVelocity.x,
            Is.EqualTo(Mathf.Min(10f * Time.fixedDeltaTime, _movement.MoveSpeed))
                .Within(0.001f));

        for (int i = 0; i < 120; i++)
        {
            _movement.Move(Vector2.right);
            _physics.Simulate(Time.fixedDeltaTime);
        }

        Assert.That(_body.linearVelocity.x,
            Is.EqualTo(_movement.MoveSpeed).Within(0.01f));
    }

    [Test]
    public void Move_TurningRemovesVelocityInPreviousDirection()
    {
        _body.linearVelocity = Vector2.up * _movement.MoveSpeed;

        for (int i = 0; i < 120; i++)
        {
            _movement.Move(Vector2.right);
            _physics.Simulate(Time.fixedDeltaTime);
        }

        Assert.That(_body.linearVelocity.x,
            Is.EqualTo(_movement.MoveSpeed).Within(0.01f));
        Assert.That(_body.linearVelocity.y, Is.EqualTo(0f).Within(0.001f));
    }

    [TestCase(5f)]
    [TestCase(40f)]
    public void Move_RightAngleTurnBrakesSidewaysWithoutChangingForwardAcceleration(
        float deceleration)
    {
        var settings = new UnityEditor.SerializedObject(_movement);
        settings.FindProperty("moveDeceleration").floatValue = deceleration;
        settings.ApplyModifiedPropertiesWithoutUndo();
        float sidewaysSpeed = _movement.MoveSpeed;
        _body.linearVelocity = Vector2.up * sidewaysSpeed;

        _movement.Move(Vector2.right);
        Assert.That(_body.linearVelocity, Is.EqualTo(Vector2.up * sidewaysSpeed));
        _physics.Simulate(Time.fixedDeltaTime);

        Assert.That(_body.linearVelocity.x,
            Is.EqualTo(Mathf.Min(10f * Time.fixedDeltaTime, _movement.MoveSpeed))
                .Within(0.001f));
        Assert.That(_body.linearVelocity.y,
            Is.EqualTo(Mathf.Max(0f, sidewaysSpeed - deceleration * Time.fixedDeltaTime))
                .Within(0.001f));
    }

    [Test]
    public void Brake_ReducesSpeedGraduallyAndStopsWithoutReversing()
    {
        var settings = new UnityEditor.SerializedObject(_movement);
        settings.FindProperty("moveSpeed").floatValue = 10f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        _body.linearVelocity = Vector2.right * 10f;

        _movement.Brake();
        Assert.That(_body.linearVelocity.x, Is.EqualTo(10f));
        _physics.Simulate(Time.fixedDeltaTime);
        Assert.That(_body.linearVelocity.x,
            Is.EqualTo(Mathf.Max(0f, 10f - 20f * Time.fixedDeltaTime))
                .Within(0.001f));

        int steps = Mathf.CeilToInt(0.5f / Time.fixedDeltaTime);
        for (int i = 0; i < steps; i++)
        {
            _movement.Brake();
            _physics.Simulate(Time.fixedDeltaTime);
        }

        Assert.That(_body.linearVelocity.magnitude, Is.LessThan(0.001f));
    }

    [Test]
    public void FrozenEnemy_DoesNotAccumulateMovementOrBrakeForce()
    {
        _body.constraints |= RigidbodyConstraints2D.FreezePosition;
        _movement.Move(Vector2.right);
        _movement.Brake();

        Assert.That(_body.totalForce, Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void NoMovementInput_AllowsImpulseAndDamping()
    {
        _body.AddForce(Vector2.up * 3f, ForceMode2D.Impulse);
        Vector2 pushedVelocity = _body.linearVelocity;
        _physics.Simulate(Time.fixedDeltaTime);
        Assert.That(_body.linearVelocity.y, Is.GreaterThan(0f));
        for (int i = 1; i < 100; i++)
        {
            _physics.Simulate(Time.fixedDeltaTime);
        }
        Assert.That(_body.position.y, Is.GreaterThan(0.1f));
        Assert.That(_body.linearVelocity.magnitude,
            Is.LessThan(pushedVelocity.magnitude * 0.01f), "감쇠로 외력 속도가 99% 이상 줄어야 한다.");
    }

    [Test]
    public void Move_PreservesExternalPushBelowSpeedLimit()
    {
        Vector2 velocity = Vector2.right * (_movement.MoveSpeed * 1.05f);
        _body.linearVelocity = velocity;
        _movement.Move(Vector2.right);
        Assert.That(_body.linearVelocity, Is.EqualTo(velocity));
    }

    [TestCase(1.1f)]
    [TestCase(1.5f)]
    public void Move_LimitsExcessivePushSpeedAndPreservesDirection(float multiplier)
    {
        var settings = new UnityEditor.SerializedObject(_movement);
        settings.FindProperty("pushSpeedLimitMultiplier").floatValue = multiplier;
        settings.ApplyModifiedPropertiesWithoutUndo();
        Vector2 velocity = new Vector2(4f, 3f);
        _body.linearVelocity = velocity;

        _movement.Move(Vector2.right);

        float limit = _movement.MoveSpeed * multiplier;
        Assert.That(_body.linearVelocity.magnitude, Is.EqualTo(limit).Within(0.001f));
        Assert.That(Vector2.Distance(_body.linearVelocity.normalized, velocity.normalized),
            Is.LessThan(0.001f));
        _physics.Simulate(Time.fixedDeltaTime);
        Assert.That(_body.linearVelocity.magnitude, Is.LessThanOrEqualTo(limit + 0.001f));
    }

    [Test]
    public void Brake_LimitsExcessivePushEvenWhenDecelerationIsZero()
    {
        var settings = new UnityEditor.SerializedObject(_movement);
        settings.FindProperty("moveDeceleration").floatValue = 0f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        _body.linearVelocity = Vector2.left * 20f;

        _movement.Brake();

        Assert.That(_body.linearVelocity.x,
            Is.EqualTo(-_movement.MoveSpeed * 1.1f).Within(0.001f));
    }

    [Test]
    public void StoppedEnemy_CanBePushedByAnotherBody()
    {
        Rigidbody2D pusher = CreateBody("Pusher", new Vector2(-1.1f, 0f));
        Physics2D.SyncTransforms();
        for (int i = 0; i < 50; i++)
        {
            // 계속 전진하는 상대와 충돌하면 제동 중인 적도 이동할 수 있어야 한다.
            pusher.linearVelocity = Vector2.right * 4f;
            _movement.Brake();
            _physics.Simulate(Time.fixedDeltaTime);
        }
        Assert.That(_body.position.x, Is.GreaterThan(0.1f));
    }

    [Test]
    public void PathCorners_RequireCloseApproachAndPreserveFinalArrivalDistance()
    {
        typeof(EnemyMovement).GetField("_path", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_movement, new[] { new Vector3(0.15f, 0f), new Vector3(0.15f, 1f) });

        Assert.That(_movement.TryGetNextPathDirection(out Vector2 direction), Is.True);
        Assert.That(direction, Is.EqualTo(Vector2.right));

        _movement.transform.position = new Vector3(0.1f, 0f);
        Physics2D.SyncTransforms();
        Assert.That(_movement.TryGetNextPathDirection(out direction), Is.True);
        Assert.That(direction.y, Is.GreaterThan(0.99f));

        _movement.transform.position = new Vector3(0.15f, 0.8f);
        Physics2D.SyncTransforms();
        Assert.That(_movement.TryGetNextPathDirection(out _), Is.False);
    }

    [Test]
    public void Move_SteersAroundStationaryBodyUsingFootCollider()
    {
        BoxCollider2D foot = _body.GetComponent<BoxCollider2D>();
        typeof(EnemyMovement).GetField("_footCollider", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_movement, foot);
        Rigidbody2D blocker = CreateBody("Blocker", new Vector2(1.1f, 0f));
        blocker.constraints = RigidbodyConstraints2D.FreezeAll;
        Physics2D.SyncTransforms();

        float largestSideStep = 0f;
        for (int i = 0; i < 600; i++)
        {
            _movement.Move((new Vector2(5f, 0f) - _body.position).normalized);
            _physics.Simulate(Time.fixedDeltaTime);
            largestSideStep = Mathf.Max(largestSideStep, Mathf.Abs(_body.position.y));
        }

        Assert.That(largestSideStep, Is.GreaterThan(1f));
        Assert.That(_body.position.x, Is.GreaterThan(3f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PathStart_AdvancesPastProjectionOnlyWithClearPhysicalRoute(bool blocked)
    {
        typeof(EnemyMovement).GetField("_footCollider", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_movement, _body.GetComponent<BoxCollider2D>());
        typeof(EnemyMovement).GetField("_path", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_movement, new[] { Vector3.zero, Vector3.right * 4f });
        _body.transform.position = Vector3.right * 0.5f;
        if (blocked)
            CreateBody("Terrain", Vector2.right * 2f).bodyType = RigidbodyType2D.Static;
        Physics2D.SyncTransforms();

        Assert.That(_movement.TryGetNextPathDirection(out Vector2 direction), Is.True);
        Assert.That(direction.x, blocked ? Is.LessThan(0f) : Is.GreaterThan(0f));
    }
    [TestCase(false)]
    [TestCase(true)]
    public void PathCorner_SkipsExactCornerWhenNextSegmentIsPhysicallyClear(bool blocked)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(EnemyMovement).GetField("_footCollider", flags)
            .SetValue(_movement, _body.GetComponent<BoxCollider2D>());
        typeof(EnemyMovement).GetField("_path", flags)
            .SetValue(_movement, new[] { Vector3.zero, new Vector3(3f, 0f), new Vector3(3f, 4f) });
        typeof(EnemyMovement).GetField("_currentPath", flags).SetValue(_movement, 1);
        _body.transform.position = new Vector2(2f, 1f);
        if (blocked)
            CreateBody("Terrain", new Vector2(2.5f, 2.5f)).bodyType = RigidbodyType2D.Static;
        Physics2D.SyncTransforms();

        Assert.That(_movement.TryGetNextPathDirection(out Vector2 direction), Is.True);
        Assert.That(direction.y, blocked ? Is.LessThan(0f) : Is.GreaterThan(0f));
    }
    private Rigidbody2D CreateBody(string name, Vector2 position)
    {
        var root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, _scene);
        var body = root.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.position = position;
        root.AddComponent<BoxCollider2D>().includeLayers = 1 << root.layer;
        return body;
    }
}
