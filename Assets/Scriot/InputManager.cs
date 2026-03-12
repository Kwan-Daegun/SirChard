using UnityEngine;

public class InputManager : MonoBehaviour
{
    public PlayerMovement player1;
    public PlayerMovement player2;

    
    public PlayerTackle tackle1;
    public PlayerTackle tackle2;

    void Update()
    {
        HandlePlayer1();
        HandlePlayer2();
    }

    void HandlePlayer1()
    {
        Vector2 move = new Vector2(
            (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
            (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)
        );

        bool jump = Input.GetKey(KeyCode.Space);

        player1.SetInput(move, jump);

        if (Input.GetKeyDown(KeyCode.F))
        {
            tackle1.TryTackle(); 
        }
    }

    void HandlePlayer2()
    {
        Vector2 move = new Vector2(
            (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0),
            (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.DownArrow) ? 1 : 0)
        );

        bool jump = Input.GetKey(KeyCode.RightControl);

        player2.SetInput(move, jump);

        if (Input.GetKeyDown(KeyCode.RightShift))
        {
            tackle2.TryTackle(); 
        }
    }
}