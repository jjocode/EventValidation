using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Net;
using System.Text;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc;
using viewer.Hubs;
using viewer.Models;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;

namespace viewer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WarrantyController : ControllerBase
    {
        private bool EventTypeSubcriptionValidation
            => HttpContext.Request.Headers["aeg-event-type"].FirstOrDefault() ==
               "SubscriptionValidation";

        private bool EventTypeNotification
            => HttpContext.Request.Headers["aeg-event-type"].FirstOrDefault() ==
               "Notification";

        private readonly IHubContext<GridEventsHub> _hubContext;

        public WarrantyController(IHubContext<GridEventsHub> gridEventsHubContext)
        {
            this._hubContext = gridEventsHubContext;
        }

        [HttpOptions]
        public async Task<IActionResult> Options()
        {
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                var webhookRequestOrigin = HttpContext.Request.Headers["WebHook-Request-Origin"].FirstOrDefault();
                var webhookRequestCallback = HttpContext.Request.Headers["WebHook-Request-Callback"];
                var webhookRequestRate = HttpContext.Request.Headers["WebHook-Request-Rate"];
                HttpContext.Response.Headers.Add("WebHook-Allowed-Rate", "*");
                HttpContext.Response.Headers.Add("WebHook-Allowed-Origin", webhookRequestOrigin);
            }

            return Ok();
        }


        [HttpPost]
        public async Task<IActionResult> Post()
        {
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                var jsonContent = await reader.ReadToEndAsync();

                // Check the event type.
                // Return the validation code if it's 
                // a subscription validation request. 
                if (EventTypeSubcriptionValidation)
                {
                    return await HandleValidation(jsonContent);
                }
                else //if (EventTypeNotification)
                {
                    return await HandleGridEvents(jsonContent);
                }

                return BadRequest();
            }
        }

        private async Task<JsonResult> HandleValidation(string jsonContent)
        {
            var gridEvent =
                JsonConvert.DeserializeObject<List<GridEvent<Dictionary<string, string>>>>(jsonContent)
                    .First();

            await this._hubContext.Clients.All.SendAsync(
                "gridupdate",
                gridEvent.Id,
                gridEvent.EventType,
                gridEvent.Subject,
                gridEvent.EventTime.ToLongTimeString(),
                jsonContent.ToString());

            // Retrieve the validation code and echo back.
            var validationCode = gridEvent.Data["validationCode"];
            return new JsonResult(new
            {
                validationResponse = validationCode
            });
        }

        private async Task<IActionResult> HandleGridEvents(string jsonContent)
        {
            var events = JArray.Parse(jsonContent);
            foreach (var e in events)
            {
                // Invoke a method on the clients for 
                // an event grid notiification.                        
                var eventGridEvent = JsonConvert.DeserializeObject<GridEvent<dynamic>>(e.ToString());

                if (eventGridEvent.EventType == "Warranty_Issued")
                {
                    await this._hubContext.Clients.All.SendAsync(
                       "gridupdate",
                       eventGridEvent.Id,
                       eventGridEvent.EventType,
                       eventGridEvent.Subject = "Warranty Controller Working",
                       eventGridEvent.EventTime.ToLongTimeString(),
                       e.ToString());
                }
                else
                {
                    await this._hubContext.Clients.All.SendAsync(
                       "gridupdate",
                       eventGridEvent.Id,
                       eventGridEvent.EventType,
                       eventGridEvent.Subject = "Error",
                       eventGridEvent.EventTime.ToLongTimeString(),
                       e.ToString());
                }
                
            }

            return Ok();
        }



        //[HttpPost]
        //public static async Task<HttpResponseMessage> Post(HttpRequestMessage req)
        //{            
        //    string response = string.Empty;
        //    string requestContent = await req.Content.ReadAsStringAsync();


        //    EventGridSubscriber eventGridSubscriber = new EventGridSubscriber();
        //    eventGridSubscriber.AddOrUpdateCustomEventMapping("Warranty_Issued", typeof(ProgressWarranty));
        //    EventGridEvent[] eventGridEvents = eventGridSubscriber.DeserializeEventGridEvents(requestContent);

        //    foreach (EventGridEvent eventGridEvent in eventGridEvents)
        //    {
        //        if (eventGridEvent.Data is SubscriptionValidationEventData)
        //        {
        //            var eventData = (SubscriptionValidationEventData)eventGridEvent.Data;

        //            var responseData = new SubscriptionValidationResponse()
        //            {
        //                ValidationResponse = eventData.ValidationCode
        //            };

        //            req.Content = new StringContent(eventData.ValidationCode);

        //            return req.CreateResponse(req.Content);

        //        }
        //        else if (eventGridEvent.Data is ProgressWarranty)
        //        {
        //            var eventData = (ProgressWarranty)eventGridEvent.Data;
        //            //handle event here


        //        }
        //    }

        //    return req.CreateResponse(HttpStatusCode.OK);
        //}

        //[HttpPost]
        //[Route("api/EventGridEventHandler")]
        //public JObject Post([FromBody]object request)
        //{
        //    var eventGridEvent = JsonConvert.DeserializeObject<EventGridEvent[]>(request.ToString())
        //        .FirstOrDefault();
        //    if (eventGridEvent == null) return new JObject(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        //    var data = eventGridEvent.Data as JObject;

        //    if (string.Equals(eventGridEvent.EventType, "Microsoft.EventGrid.SubscriptionValidationEvent", StringComparison.OrdinalIgnoreCase))
        //    {
        //        if (data != null)
        //        {
        //            var eventData = data.ToObject<SubscriptionValidationEventData>();
        //            var responseData = new SubscriptionValidationResponse
        //            {
        //                ValidationResponse = eventData.ValidationCode
        //            };

        //            if (responseData.ValidationResponse != null)
        //            {
        //                return JObject.FromObject(responseData);
        //            }
        //        }
        //    }
        //    else
        //    {
        //        if (data == null) return new JObject(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        //        var eventData = data.ToObject<CustomData>();
        //        var customEvent = CustomEvent<CustomData>.CreateCustomEvent(eventData);
        //        return JObject.FromObject(customEvent);
        //    }

        //    return new JObject(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        //}

        //// GET: api/Warranty
        //[HttpGet]
        //public IEnumerable<string> Get()
        //{
        //    return new string[] { "value1", "value2" };
        //}

        //// GET: api/Warranty/5
        //[HttpGet("{id}", Name = "Get")]
        //public string Get(int id)
        //{
        //    return "value";
        //}

        //// POST: api/Warranty
        //[HttpPost]
        //public void Post([FromBody] string value)
        //{
        //}

        //// PUT: api/Warranty/5
        //[HttpPut("{id}")]
        //public void Put(int id, [FromBody] string value)
        //{
        //}

        //// DELETE: api/ApiWithActions/5
        //[HttpDelete("{id}")]
        //public void Delete(int id)
        //{
        //}
    }
}
