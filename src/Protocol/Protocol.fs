namespace Protocol

type MessageType =
    | Broadcast of string
    | Private of int * string
    | Login of string
    | LoginError of string

module MessageHandling=

    let settings = let s = new Newtonsoft.Json.JsonSerializerSettings()
                   s.TypeNameHandling <- Newtonsoft.Json.TypeNameHandling.All
                   s

    let handleMessage 
            broadcastHandler
            privateHandler
            loginHandler
            loginErrorHandler
            senderId
            jsonString =
                try
                    match Newtonsoft.Json.JsonConvert.DeserializeObject<MessageType>(jsonString, settings) with
                    | Broadcast msg             -> broadcastHandler senderId msg
                    | Private (recieverId, msg) -> privateHandler recieverId senderId msg
                    | Login Id -> loginHandler id
                    | LoginError msg -> loginErrorHandler msg
                with
                |   ex -> 
                    printfn "%s" (ex.ToString())
                    failwith ex.Message
     

    let serializeMessage msg =
        Newtonsoft.Json.JsonConvert.SerializeObject(msg,settings)