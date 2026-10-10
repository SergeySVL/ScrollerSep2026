#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class ShopPlayModeChecks : MonoBehaviour
{
    [Serializable] private class Check { public string name; public bool passed; public string details; }
    [Serializable] private class Report { public string timestamp; public List<Check> checks=new List<Check>(); public List<string> runtimeErrors=new List<string>(); }
    private const string ReportFolder = "C:/Users/MrDer/Documents/Codex/2026-09-06/computer-plugin-computer-use-openai-bundled-6/work/sidescroller";
    private readonly Report report=new Report();
    private Keyboard keyboard;
    private ShopPlayerController player;
    private Rigidbody2D body;
    private bool oldBackground;
    private float floor;

    private IEnumerator Start()
    {
        Application.logMessageReceived += Log;
        oldBackground=Application.runInBackground;
        Application.runInBackground=true;
        player=FindAnyObjectByType<ShopPlayerController>();
        if(!player){CheckThat("Player exists",false,"Player component missing");Finish();yield break;}
        body=player.GetComponent<Rigidbody2D>();
        keyboard=InputSystem.AddDevice<Keyboard>("ShopVerificationKeyboard");
        Keys();
        yield return Wait(.8f);
        floor=GameObject.Find("Floor - aligned to metal deck").GetComponent<BoxCollider2D>().bounds.max.y;
        CheckThat("Grounded on visible floor",player.Grounded && Mathf.Abs(body.position.y-floor)<.07f,"feet="+body.position.y+" floor="+floor);
        yield return MoveCheck("D moves right",Key.D,1);
        yield return MoveCheck("A moves left",Key.A,-1);
        yield return MoveCheck("Right arrow moves right",Key.RightArrow,1);
        yield return MoveCheck("Left arrow moves left",Key.LeftArrow,-1);
        Keys(Key.A,Key.D);yield return Wait(.25f);
        CheckThat("Opposite inputs cancel",Mathf.Abs(body.linearVelocity.x)<.05f,"vx="+body.linearVelocity.x);
        Keys();yield return Wait(.1f);
        float baseline=body.position.y;
        float maxHeight=baseline;
        int launches=0; bool previous=player.Grounded;
        Keys(Key.Space);
        float end=Time.time+1.6f;
        while(Time.time<end)
        {
            maxHeight=Mathf.Max(maxHeight,body.position.y);
            if(previous&&!player.Grounded)launches++;
            previous=player.Grounded;
            yield return null;
        }
        CheckThat("Space jumps and held key does not auto-jump",maxHeight-baseline>.7f && launches==1 && player.Grounded,"rise="+(maxHeight-baseline)+" launches="+launches);
        Keys();yield return Wait(.15f);
        foreach(var key in new[]{Key.W,Key.UpArrow})
        {
            Keys(key);yield return Wait(.12f);
            CheckThat(key+" jumps",!player.Grounded&&body.position.y>floor+.2f,"y="+body.position.y);
            Keys();yield return Wait(.9f);
        }
        Keys(Key.Space);yield return Wait(.1f);Keys();yield return Wait(.08f);
        float beforeAirJump=body.linearVelocity.y;
        Keys(Key.Space);yield return Wait(.08f);
        CheckThat("Air jump is rejected",body.linearVelocity.y<beforeAirJump,"vy before="+beforeAirJump+" after="+body.linearVelocity.y);
        Keys();yield return Wait(1f);
        foreach(var key in new[]{Key.S,Key.DownArrow})
        {
            Keys(key);yield return Wait(.15f);
            CheckThat(key+" crouches",player.Crouching&&player.GetComponent<CapsuleCollider2D>().size.y<1,"height="+player.GetComponent<CapsuleCollider2D>().size.y);
            Keys();yield return Wait(.15f);
            CheckThat("Stand after "+key,!player.Crouching,"height="+player.GetComponent<CapsuleCollider2D>().size.y);
        }
        Keys(Key.D,Key.LeftShift);yield return Wait(.25f);
        CheckThat("Shift sprints",body.linearVelocity.x>player.walkSpeed+.5f,"vx="+body.linearVelocity.x);
        Keys();yield return Wait(.25f);
        Keys(Key.J);yield return Wait(.15f);
        CheckThat("J plays the sword frames",player.CurrentAnimation=="Action",player.GetComponent<SpriteRenderer>().sprite.name);
        Keys();yield return Wait(.9f);
        CheckThat("Action returns to idle",player.CurrentAnimation=="Idle",player.CurrentAnimation);
        Keys(Key.D,Key.LeftShift);yield return Wait(3f);Keys();yield return Wait(.2f);
        float right=GameObject.Find("Right boundary").GetComponent<BoxCollider2D>().bounds.min.x;
        CheckThat("Right wall contains player",body.position.x<right-.25f&&body.position.x>right-1f,"x="+body.position.x+" wall="+right);
        Keys(Key.A,Key.LeftShift);yield return Wait(3.3f);Keys();yield return Wait(.2f);
        float left=GameObject.Find("Left boundary").GetComponent<BoxCollider2D>().bounds.max.x;
        CheckThat("Left wall contains player",body.position.x>left+.25f&&body.position.x<left+1f,"x="+body.position.x+" wall="+left);
        Keys(Key.R);yield return Wait(.4f);Keys();
        CheckThat("R returns to spawn",Mathf.Abs(body.position.x-player.RespawnPoint.x)<.05f&&player.Grounded,"position="+body.position);
        body.position=new Vector2(body.position.x,player.killY-1f);yield return Wait(.5f);
        CheckThat("Fall recovery",Mathf.Abs(body.position.x-player.RespawnPoint.x)<.05f&&player.Grounded,"position="+body.position);
        CheckThat("Animation is an individual frame",player.GetComponent<SpriteRenderer>().sprite.rect.width<32,"sprite="+player.GetComponent<SpriteRenderer>().sprite.name);
        ScreenCapture.CaptureScreenshot(Path.Combine(ReportFolder,"play-mode-verified.png"));
        yield return Wait(.3f);
        Finish();
    }
    private IEnumerator MoveCheck(string name,Key key,float direction)
    {
        float x=body.position.x;var frame=player.GetComponent<SpriteRenderer>().sprite;
        Keys(key);yield return Wait(.4f);
        CheckThat(name,(body.position.x-x)*direction>1f&&player.CurrentAnimation=="Run","dx="+(body.position.x-x)+" animation="+player.CurrentAnimation);
        CheckThat(name+" animates",player.GetComponent<SpriteRenderer>().sprite!=frame,player.GetComponent<SpriteRenderer>().sprite.name);
        Keys();yield return Wait(.2f);
        CheckThat(name+" release stops",Mathf.Abs(body.linearVelocity.x)<.05f,"vx="+body.linearVelocity.x);
    }
    private void Keys(params Key[] keys) { if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys)); }
    private IEnumerator Wait(float seconds){float until=Time.time+seconds;while(Time.time<until)yield return null;}
    private void CheckThat(string name,bool passed,string details){report.checks.Add(new Check{name=name,passed=passed,details=details});}
    private void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.runtimeErrors.Add(message);}
    private void Finish()
    {
        CheckThat("No runtime errors",report.runtimeErrors.Count==0,string.Join("\n",report.runtimeErrors));
        report.timestamp=DateTime.Now.ToString("O");
        Directory.CreateDirectory(ReportFolder);
        File.WriteAllText(Path.Combine(ReportFolder,"play-mode-checks.json"),JsonUtility.ToJson(report,true));
        Cleanup();
        Debug.Log("SHOP PLAY MODE CHECKS FINISHED: "+report.checks.Count+" checks. See play-mode-checks.json.");
        EditorApplication.isPlaying=false;
    }
    private void Cleanup()
    { Application.logMessageReceived-=Log;Application.runInBackground=oldBackground;if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;} }
    private void OnDestroy(){Cleanup();}
}

#endif
