namespace Protocol

type MessageType =
    |Broadcast of string
    |Private of int * string

module MessageHandling=

    let settings = let s = new Newtonsoft.Json.JsonSerializerSettings()
                   s.TypeNameHandling <- Newtonsoft.Json.TypeNameHandling.All
                   s


    let handleMessage (broadcastHandler:string -> Async<unit>) privateHanlder jsonString =
        try
            match Newtonsoft.Json.JsonConvert.DeserializeObject<MessageType>(jsonString, settings) with
            | Broadcast msg     -> broadcastHandler msg
            | Private (id, msg) -> privateHanlder id msg
        with
        |   ex -> 
            printfn "%s" (ex.ToString())
            failwith ex.Message
     

    let serializeMessage msg =
        Newtonsoft.Json.JsonConvert.SerializeObject(msg,settings)