using UnityEngine;
using System.Collections;

[AddComponentMenu("Camera-Control/Mouse Orbit with zoom")]
public class MouseOrbitImproved : MonoBehaviour
{

	public Transform target;
	public Transform preview;
	public float distance = 5.0f;
	public float xSpeed = 120.0f;
	public float ySpeed = 120.0f;

	public float yMinLimit = -20f;
	public float yMaxLimit = 80f;

	public float distanceMin = .5f;
	public float distanceMax = 15f;

	private Rigidbody r;

	private bool startedOverUI = false;

	float x = 0.0f;
	float y = 0.0f;

	float offset_x = 0.0f;
	float offset_y = 0.0f;

	// Use this for initialization
	void Start()
	{
		Vector3 angles = transform.eulerAngles;
		x = angles.y;
		y = angles.x;

		r = GetComponent<Rigidbody>();

		// Make the rigid body not change rotation
		if (r != null)
		{
			r.freezeRotation = true;
		}
	}

	void LateUpdate()
	{
		if (target && !startedOverUI)
		{
			offset_x = 0f;
			offset_y = 0f;
			if (Input.GetMouseButton(0))
			{
				x += Input.GetAxis("Mouse X") * xSpeed * 0.02f;
				y -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;
			}
			else if (Input.GetMouseButton(2))
			{
				offset_x = -Input.GetAxis("Mouse X") * xSpeed * 0.02f;
				offset_y = -Input.GetAxis("Mouse Y") * ySpeed * 0.02f;
			}

			target.rotation = transform.rotation;

			var offset = new Vector3(offset_x, offset_y, 0);
			target.Translate(offset, Space.Self);
			transform.Translate(offset, Space.Self);

			y = ClampAngle(y, yMinLimit, yMaxLimit);

			Quaternion rotation = Quaternion.Euler(y, x, 0);

			if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
				distance = Mathf.Clamp(distance - Input.GetAxis("Mouse ScrollWheel") * distance, distanceMin, distanceMax);

			// RaycastHit hit;
			// if (Physics.Linecast (target.position, transform.position, out hit)) 
			// {
			//     distance -=  hit.distance;
			// }
			Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
			Vector3 position = rotation * negDistance + target.position;

			transform.rotation = rotation;
			transform.position = position;
		}
	}

	void Update()
	{
		if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(2))
		{
			startedOverUI = UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
		}
		// else if (!Input.GetMouseButton(0) && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
		// {
		// 	startedOverUI = true;
		// }
		else if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonDown(2))// || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
			startedOverUI = false;

	}

	public static float ClampAngle(float angle, float min, float max)
	{
		if (angle < -360F)
			angle += 360F;
		if (angle > 360F)
			angle -= 360F;
		return Mathf.Clamp(angle, min, max);
	}
}