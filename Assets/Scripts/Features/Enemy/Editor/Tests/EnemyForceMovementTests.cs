using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>AddForce 이동과 이동 입력 중단 후 외부 힘·충돌의 보존을 검사한다.</summary>
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
        Assert.That(_body.linearVelocity.x, Is.InRange(1f, _movement.MoveSpeed + 0.01f));

        for (int i = 0; i < 100; i++)
        {
            _movement.Move(Vector2.left);
            _physics.Simulate(Time.fixedDeltaTime);
        }
        Assert.That(_body.linearVelocity.x, Is.LessThan(0f));
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
    public void Move_DoesNotClampVelocityFromExternalPush()
    {
        _body.linearVelocity = new Vector2(4f, 3f);
        _movement.Move(Vector2.right);
        Assert.That(_body.linearVelocity, Is.EqualTo(new Vector2(4f, 3f)));
        _physics.Simulate(Time.fixedDeltaTime);
        Assert.That(_body.linearVelocity.x, Is.GreaterThan(_movement.MoveSpeed));
        Assert.That(_body.linearVelocity.y, Is.GreaterThan(0f));
    }

    [Test]
    public void StoppedEnemy_CanBePushedByAnotherBody()
    {
        Rigidbody2D pusher = CreateBody("Pusher", new Vector2(-1.1f, 0f));
        pusher.AddForce(Vector2.right * 4f, ForceMode2D.Impulse);
        Physics2D.SyncTransforms();
        for (int i = 0; i < 50; i++)
        {
            _physics.Simulate(Time.fixedDeltaTime);
        }
        Assert.That(_body.position.x, Is.GreaterThan(0.1f));
    }

    private Rigidbody2D CreateBody(string name, Vector2 position)
    {
        var root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, _scene);
        var body = root.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.position = position;
        root.AddComponent<BoxCollider2D>();
        return body;
    }
}
