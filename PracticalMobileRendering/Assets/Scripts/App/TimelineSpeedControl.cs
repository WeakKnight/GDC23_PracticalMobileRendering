using UnityEngine;
using UnityEngine.Playables;

public class TimelineSpeedControl : MonoBehaviour
{
    [SerializeField]
    public float speed = 1.0f;
    // Start is called before the first frame update
    void Start()
    {
        var pd = GetComponent<PlayableDirector>();
        pd.playableGraph.GetRootPlayable(0).SetSpeed(speed);
    }
}
