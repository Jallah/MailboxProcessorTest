namespace Protocol

type MessageType =
    |Broadcast of string
    |Private of int * string

module MessageHandling=

    let handleMessage (broadcastHandler:string -> Async<unit>) privateHanlder jsonString =
        match Newtonsoft.Json.JsonConvert.DeserializeObject<MessageType>(jsonString) with
        | Broadcast msg     -> broadcastHandler msg
        | Private (id, msg) -> privateHanlder id msg

    let serializeMessage msg =
        let settings = new Newtonsoft.Json.JsonSerializerSettings()
        settings.TypeNameHandling <- Newtonsoft.Json.TypeNameHandling.All
        Newtonsoft.Json.JsonConvert.SerializeObject(msg,settings)