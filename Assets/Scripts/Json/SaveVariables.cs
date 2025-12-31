using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveVariables
{
    public bool save;
    public int coin;
    public int adsCoin;
    public int health;
    public string lastAdsResetDate;
    public List<int> starCounts;
    public List<bool> unlock;
    public List<Items> items;
}