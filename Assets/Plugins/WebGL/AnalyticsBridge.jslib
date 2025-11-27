mergeInto(LibraryManager.library, {
  IncrementSceneCounter: function (idPtr, namePtr, index) {
    var id   = UTF8ToString(idPtr);
    var name = (namePtr ? UTF8ToString(namePtr) : "") || "";

    try {
      if (!window.firebase || !firebase.apps || !firebase.apps.length) {
        console.warn("[analytics] firebase not ready");
        return;
      }
      var db = firebase.database();

      // UTC day key: YYYYMMDD
      var now = new Date();
      var y = now.getUTCFullYear();
      var m = String(now.getUTCMonth() + 1).padStart(2, "0");
      var d = String(now.getUTCDate()).padStart(2, "0");
      var dayKey = "" + y + m + d;

      // ---- lifetime (global) ----
      var globalRef = db.ref("counters/globalTotals/" + id);
      globalRef.child("title").set(name);
      globalRef.child("index").set(index);
      globalRef.child("views").transaction(function (x) { return (x || 0) + 1; });

      // ---- per day ----
      var dailyRef = db.ref("counters/daily/" + dayKey + "/" + id);
      dailyRef.child("title").set(name);
      dailyRef.child("index").set(index);
      dailyRef.child("views").transaction(function (x) { return (x || 0) + 1; });

      console.log("[analytics] counter++", id, name, index);
    } catch (e) {
      console.error("[analytics] IncrementSceneCounter error:", e);
    }
  },

  LogSceneVisit: function (idPtr, namePtr, index) {
    var id   = UTF8ToString(idPtr);
    var name = (namePtr ? UTF8ToString(namePtr) : "") || "";

    try {
      if (!window.firebase || !firebase.apps || !firebase.apps.length) {
        console.warn("[analytics] firebase not ready");
        return;
      }
      var db = firebase.database();

      // visits/logs/YYYY/MM/DD/{sceneId}/{pushId}
      var now = new Date();
      var y = now.getUTCFullYear();
      var m = String(now.getUTCMonth() + 1).padStart(2, "0");
      var d = String(now.getUTCDate()).padStart(2, "0");
      var path = "visits/logs/" + y + "/" + m + "/" + d + "/" + id;

      db.ref(path).push({ 
        title: name, 
        index: index, 
        ts: Date.now() 
      });
      console.log("[analytics] visit", id, name, index);
    } catch (e) {
      console.error("[analytics] LogSceneVisit error:", e);
    }
  },

   GetTopVisitedScenes: function (unityObjPtr, callbackNamePtr) {
    var unityObj = UTF8ToString(unityObjPtr);
    var callbackName = UTF8ToString(callbackNamePtr);

    try {
      if (!window.firebase || !firebase.apps || !firebase.apps.length) {
        console.warn("[analytics] firebase not ready");
        return;
      }

      var db = firebase.database();
      var ref = db.ref("counters/globalTotals");

      ref.once("value").then(function (snapshot) {
        var all = [];
        snapshot.forEach(function (child) {
          var val = child.val();
          if (val && typeof val.index === "number") {
            all.push({
              index: val.index,
              title: val.title || "(no title)",
              views: val.views || 0
            });
          }
        });

        // Sort by views (descending)
        all.sort(function (a, b) { return b.views - a.views; });

        // Keep top 10
        var top = all.slice(0, 10);

        // Return as JSON to Unity
        var json = JSON.stringify(top);
        SendMessage(unityObj, callbackName, json);

        console.log("[analytics] sent top visited scenes:", top);
      });
    } catch (e) {
      console.error("[analytics] GetTopVisitedScenes error:", e);
    }
  }
});
