#r "Newtonsoft.Json"

using System;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
//using System.Text.Json.Serialization;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.Azure.WebJobs;
//using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    //log.LogInformation("C# HTTP trigger function processed a request.");
    

    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    //log.LogInformation("Data received:\n"+requestBody);
    string destFeatureLayer = "https://services1.arcgis.com/1YRV70GwTj9GYxWK/ArcGIS/rest/services/inReach_log/FeatureServer/0";
    string payload = "";
    if ( !requestBody.Contains("Events")){
        return new BadRequestObjectResult("This is not inReach Event data.");
    }
    

    //TODO convert inReach json in requestBody to feature json
    inReachJson inreach = JsonConvert.DeserializeObject<inReachJson>(requestBody);
    inReachFeatures irfs = new inReachFeatures();
    List<inReachFeature> fs = new List<inReachFeature>();

    irfs.Version = inreach.Version;
    foreach (inReachEvent e in inreach.Events){
        inReachFeature irf = new inReachFeature();
        irf.attributes = new inReachAttributes();
        irf.geometry = new inReachGeometry();
        irf.attributes.imei = e.imei;
        irf.attributes.messagecode = e.messageCode;
        irf.attributes.freetext_ = e.freeText;
        irf.attributes.timestamp = e.timestamp;  
        List<string> addrs = new List<String>();   
        foreach (inReachAddress addr in e.addresses){
            addrs.Add(addr.address);
        }    
        irf.attributes.addresses = string.Join(", ", addrs);       
        irf.attributes.latitude = e.point.latitude;
        irf.attributes.longitude = e.point.longitude;
        irf.attributes.altitude = e.point.altitude;
        irf.attributes.gpsfix = e.point.gpsFix;
        irf.attributes.course = e.point.course;
        irf.attributes.speed = e.point.speed;
        irf.attributes.autonomous = e.status.autonomous;
        irf.attributes.lowbattery = e.status.lowBattery;
        irf.attributes.intervalchange = e.status.intervalChange;
        irf.attributes.resetdetected = e.status.resetDetected;  
        irf.geometry.x = e.point.longitude;           
        irf.geometry.y = e.point.latitude;
        irf.geometry.z = e.point.altitude;
        fs.Add(irf);
    }
    irfs.features = fs;
    payload = JsonConvert.SerializeObject(irfs.features);                
                
    //log.LogInformation(payload);
    var value = new Dictionary<string, string>
    {
        { "features", payload },
        { "f", "pjson" }
    };
    var stringContent = new FormUrlEncodedContent(value);
    
    

    HttpClient client = new HttpClient();
    String uri = destFeatureLayer+"/addFeatures";
    var result = await client.PostAsync(uri,stringContent);
    string resultContent = await result.Content.ReadAsStringAsync();
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

public class inReachFeatures{
    public String Version {get; set; }
    public IList<inReachFeature> features {get; set; }
}

public class inReachFeature{
    public inReachAttributes attributes {get;set;}
    public inReachGeometry geometry {get;set;}
}

public class inReachAttributes{
    public String imei {get; set; }
    public Int16 messagecode {get; set; }
    public String freetext_ {get; set; }
    public Int64 timestamp {get; set; }
    public String addresses {get; set; }
    public Double latitude {get; set; }
    public Double longitude {get; set; }
    public Double altitude {get; set; }
    public Int16 gpsfix {get; set; }
    public Double course {get; set; }
    public Double speed {get; set; }
    public Int16 autonomous {get; set; }
    public Int16 lowbattery {get; set; }
    public Int16 intervalchange {get; set; }
    public Int16 resetdetected {get; set; }  
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
