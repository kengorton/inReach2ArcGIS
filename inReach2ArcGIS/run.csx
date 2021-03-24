#r "Newtonsoft.Json"

using System;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.Azure.WebJobs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    //log.LogInformation("C# HTTP trigger function processed a request.");
    //the uurl to the feature service and layer where the inReach data are to be stored
    string destFeatureLayer = "https://services1.arcgis.com/1YRV70GwTj9GYxWK/ArcGIS/rest/services/inReach_log/FeatureServer/0";
    
    //set keepLatest = true if keeping only the latest inReach feature for each device 
    //set keepLatest = false if archiving all inReach features
    bool keepLatest = true;

    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();    
    
    HttpClient client = new HttpClient();
    
    
    if ( !requestBody.Contains("Events")){
        return new BadRequestObjectResult("This is not inReach Event data.");
    }      

    // convert request body to an inReachJson object (defined below)
    inReachJson inreach = JsonConvert.DeserializeObject<inReachJson>(requestBody);
    
    List<inReachFeature> addList = new List<inReachFeature>();
    List<inReachFeature> updateList = new List<inReachFeature>();
    
        
    foreach (inReachEvent e in inreach.Events){        
        
        inReachFeature irf = new inReachFeature();
        irf.attributes = new Dictionary<string,object>();
        irf.geometry = new inReachGeometry();        

        //the update boolean indicates whether this feature is new and should be added (false) 
        //or is an existing feature and will be updated (true)
        //initial value is false (feature will be added)
        //if keepLatest is set to true and a query for the imei returns a feature
        //update will be changed to true
        bool update = false;
        if (keepLatest){
            String qUri = $"{destFeatureLayer}/query?where=imei = {e.imei}&outFields=*&returnIdsOnly=true&f=pjson";
            var qResult = await client.GetAsync(qUri);
            string qResultContent = await qResult.Content.ReadAsStringAsync();
            dynamic qResponse = JsonConvert.DeserializeObject(qResultContent);
            if (qResponse["objectIds"].Count > 0){ 
                String idField = qResponse["objectIdFieldName"];
                irf.attributes[idField] = qResponse["objectIds"][0]; 
                update = true;             
            }
        }
        irf.attributes["imei"] = e.imei;
        irf.attributes["messagecode"] = e.messageCode;
        irf.attributes["freetext_"] = e.freeText;
        irf.attributes["timestamp"] = e.timestamp;  
        List<string> addrs = new List<String>();   
        foreach (inReachAddress addr in e.addresses){
            addrs.Add(addr.address);
        }    
        irf.attributes["addresses"] = string.Join(", ", addrs);       
        irf.attributes["latitude"] = e.point.latitude;
        irf.attributes["longitude"] = e.point.longitude;
        irf.attributes["altitude"] = e.point.altitude;
        irf.attributes["gpsfix"] = e.point.gpsFix;
        irf.attributes["course"] = e.point.course;
        irf.attributes["speed"] = e.point.speed;
        irf.attributes["autonomous"] = e.status.autonomous;
        irf.attributes["lowbattery"] = e.status.lowBattery;
        irf.attributes["intervalchange"] = e.status.intervalChange;
        irf.attributes["resetdetected"] = e.status.resetDetected;  
        irf.geometry.x = e.point.longitude;           
        irf.geometry.y = e.point.latitude;
        irf.geometry.z = e.point.altitude; 

        
        if (update){
            updateList.Add(irf);
        }
        else{
            addList.Add(irf);
        }
    }
     
    //convert the inFeatures to a string
    var addload = JsonConvert.SerializeObject(addList);
    var updateload = JsonConvert.SerializeObject(updateList);    
    
    //create the dictionary for the POST request to the feature service addFeatures endpoint
    var payload = new Dictionary<string, string>
    {
        { "features", addload},
        { "f", "json" }
    };
    if (keepLatest){    
        payload = new Dictionary<string, string>
        {
            { "adds", addload},
            { "updates", updateload },
            { "f", "json" }
        };
    }    
    
    
    var stringContent = new FormUrlEncodedContent(payload);    
    String requestVerb = keepLatest ? "/applyEdits": "/addFeatures";

    String uri = destFeatureLayer + requestVerb;
    var result = await client.PostAsync(uri,stringContent);
    string resultContent = await result.Content.ReadAsStringAsync();
    dynamic details = JsonConvert.DeserializeObject(resultContent);
    if (details["error"] != null){
        return new BadRequestObjectResult(String.Format("The ArcGIS portal returned an error. {0}",resultContent));
    }
    client = null;
    return new OkObjectResult(resultContent);
}

public class inReachJson{
    public string Version { get; set; }
    public IList<inReachEvent> Events { get; set; }
}

public class inReachEvent
{        
    public string imei { get; set; }
    public Int16 messageCode { get; set; }
    public string freeText { get; set; }
    public Int64 timestamp {get; set; }
    public IList<inReachAddress> addresses {get; set; }
    public inReachPoint point {get; set; }
    public inReachStatus status {get; set; }
}

public class inReachAddress
{
    public String address { get; set;}
}   

public class inReachPoint
{
    public Double latitude { get; set;}
    public Double longitude { get; set;}
    public Double altitude { get; set;}
    public Int16 gpsFix { get; set;}
    public Double course { get; set;}
    public Double speed { get; set;}
}

public class inReachStatus
{    
    public Int16 autonomous { get; set;}
    public Int16 lowBattery { get; set;}
    public Int16 intervalChange { get; set;}
    public Int16 resetDetected { get; set;}
}  

public class inReachFeature{
    public Dictionary<string,object> attributes {get; set;}
    public inReachGeometry geometry {get;set;}
}

public class inReachGeometry{
    public Double x {get; set; }
    public Double y {get; set; }
    public Double z {get; set; }
    public inReachSR spatialReference {get; set; }
    public inReachGeometry(){
        spatialReference = new inReachSR();
    }
}

public class inReachSR{
    public Int16 wkid {get;} = 4326;
}
