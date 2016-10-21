namespace Protocol

//type MessageType =
//    | Broadcast of string * string
//    | Private of string * string
//    | Login of string
//    | LoginError of string
//    | LoginSuccess of string

type BroadCastMessage = {Sender:string; Message:string}
type PrivateMessage = {Recipient:string; Message:string}
type LoginMessage = {Name:string}
type LoginErrorMessage = {Message:string}
type LoginSuccessMessage = {Message:string}



type private Message = {Message:System.Type; Handler:obj -> Async<unit>}
type Message<'a> = {Message:'a; MessageHandler:'a -> Async<unit>}

module MessageHandling =

    let broadCastHandler msg =  async {
                                    let {Sender=_; Message=broadcast} = msg
                                    printfn "Broadcast: %s" broadcast
                                }

    let logHandler<'a> (msg:'a) = async { printfn "MSG: %A" msg }

    
    let settings = let s = new Newtonsoft.Json.JsonSerializerSettings()
                   s.TypeNameHandling <- Newtonsoft.Json.TypeNameHandling.All
                   s

    let private toUntyped message =
        let { Message=msg; MessageHandler=handleFunc } = message
        { Message=msg.GetType(); Handler=fun (arg:obj) -> handleFunc (arg :?> 'a) }

    let mutable Messages:Message list = []

    let registerMessage msg =
        match tryGetMessage
        Messages <- (toUntyped msg) :: Messages
        
    let private tryGetMessage msgType = 
                Messages
                |> List.tryFind (fun msg -> msg.Message = msgType.GetType())

//    let Messages = [ {message=typeof<BroadCastMessage>; handler= toUntyped broadCastHandler }
//                     {message=typeof<PrivateMessage>; handler=logHandler }
//                     {message=typeof<LoginMessage>; handler=logHandler }
//                     {message=typeof<LoginErrorMessage>; handler=logHandler }
//                     {message=typeof<LoginSuccessMessage>; handler=logHandler }]

    

    let getMsgHandler jsonString =
        let deserializedMsg = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonString, settings);
        (getMessageByType deserializedMsg).MessageHandler deserializedMsg
    

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