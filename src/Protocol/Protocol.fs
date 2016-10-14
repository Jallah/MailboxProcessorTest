namespace Protocol

type MessageType =
    | Broadcast of string * string
    | Private of string * string
    | Login of string
    | LoginError of string
    | LoginSuccess of string

module MessageHandling =

    let settings = let s = new Newtonsoft.Json.JsonSerializerSettings()
                   s.TypeNameHandling <- Newtonsoft.Json.TypeNameHandling.All
                   s

    let handleMessage broadcastHandler privateHandler loginHandler loginErrorHandler loginSuccessHandler senderId jsonString =
                try
                    match Newtonsoft.Json.JsonConvert.DeserializeObject<MessageType>(jsonString, settings) with
                    | Broadcast (_, msg)        -> broadcastHandler senderId msg
                    | Private (recieverId, msg) -> privateHandler recieverId senderId msg
                    | Login Id                  -> loginHandler Id
                    | LoginError msg            -> loginErrorHandler msg
                    | LoginSuccess msg          -> loginSuccessHandler msg

                with
                |   ex -> 
                    printfn "%s" (ex.ToString())
                    failwith ex.Message
     

    let serializeMessage msg =
        Newtonsoft.Json.JsonConvert.SerializeObject(msg,settings)