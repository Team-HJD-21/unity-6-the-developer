using UnityEngine;

public class SamplePlayer : MonoBehaviour
{
    private float xInput;
    private float yInput;
    
    [SerializeField] private Rigidbody2D _rb;
    [SerializeField] private Camera _camera;

    private PocInteractableViewer nearbyViewer;
    
    // Update is called once per frame
    void Update()
    {
        xInput = Input.GetAxis("Horizontal");
        yInput = Input.GetAxis("Vertical");

        if (Input.GetKeyDown(KeyCode.E) && nearbyViewer != null)
        {
            nearbyViewer.Interact();
        }
    }

    void FixedUpdate()
    {
        if (xInput != 0 || yInput != 0)
        {
            Vector2 dir = new Vector2(xInput, yInput);
            _rb.MovePosition(_rb.position + dir * 30f * Time.deltaTime);
        }
        
        _camera.transform.position = new Vector3(_rb.position.x, _rb.position.y, -10f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("PocInteractArea"))
        {
            PocInteractableViewer viewer = other.GetComponentInParent<PocInteractableViewer>();
            if (viewer == null)
            {
                return;
            }

            if (nearbyViewer != null && nearbyViewer != viewer)
            {
                nearbyViewer.ShowPrompt(false);
            }

            nearbyViewer = viewer;
            nearbyViewer.ShowPrompt(true);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (nearbyViewer == null ||
            other.GetComponentInParent<PocInteractableViewer>() != nearbyViewer)
        {
            return;
        }

        nearbyViewer.ShowPrompt(false);
        nearbyViewer = null;
    }
}
