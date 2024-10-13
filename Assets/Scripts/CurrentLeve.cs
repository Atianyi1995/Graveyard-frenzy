using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


public class CurrentLeve : MonoBehaviour
{
    public int currentlevel;
    public List<GameObject> levels = new List<GameObject>();
    // Start is called before the first frame update
    void Start()
    {
        StartLevel();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void SaveLevel()
    {
        currentlevel++;
        PlayerPrefs.SetInt("CL", currentlevel);
    }
    public void StartLevel()
    {
        if (PlayerPrefs.HasKey("CL"))
            currentlevel = PlayerPrefs.GetInt("CL");
        else
            currentlevel = 0;

        levels[currentlevel].SetActive(true);

    }
}
