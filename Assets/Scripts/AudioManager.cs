using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Unity.VisualScripting.Member;

public class AudioManager : MonoBehaviour
{
    AudioSource chessPieceSound;
    [SerializeField] AudioClip move, hit, transformation, pick;
    void Start()
    {
        chessPieceSound = GetComponent<AudioSource>();
    }
    public void Move()
    {
        if (PlayerPrefs.GetInt("Audio") == 1)
        {
            chessPieceSound.clip = move;
            chessPieceSound.Play();
        }
    }
    public void Hit()
    {
        if (PlayerPrefs.GetInt("Audio") == 1)
        {
            chessPieceSound.clip = hit;
            chessPieceSound.Play();
        }
    }
    public void Transformation()
    {
        if (PlayerPrefs.GetInt("Audio") == 1)
        {
            chessPieceSound.clip = transformation;
            chessPieceSound.Play();
        }
    }
    public void Pick()
    {
        if (PlayerPrefs.GetInt("Audio") == 1)
        {
            chessPieceSound.clip = pick != null ? pick : move;
            chessPieceSound.Play();
        }
    }
}
