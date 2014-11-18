open System
open System.IO
open Nancy
open Nancy.Hosting.Self
open FunScript
open FunScript.TypeScript
open FunScript.HTML


type AssemblyType = {name:string; isClass:bool; }

let getAssemblyTypes = 
    System.Reflection.Assembly.GetExecutingAssembly().GetTypes()
        |> Seq.map (fun m -> {name = m.Name; isClass = m.IsClass; })


[<ReflectedDefinition>]
module Web =
    type AppState = {reload:bool; types:AssemblyType array;}

    let rec mainLoop (st: RactiveState<AppState>) = async {
        let! st = async {
            match st.ractive.get "reload" :?> bool with
            | true ->
                let url = "http://localhost:6543/data"
                let req = System.Net.WebRequest.Create(url)
                let! asmTypes = req.AsyncGetJSON<AssemblyType array>()
                Globals.console.log asmTypes
                Globals.console.log asmTypes.[0].name
                return RactiveState( st, {st.data with reload = false; types  = asmTypes} )

            | false ->
                return st
        }

        return! mainLoop st
    }

    let start () =
        let ractive = Globals.Ractive.CreateFast("#ractive-container", "#ractive-template")
        RactiveState.init(ractive, { reload = true; types = [||]; })
            |> mainLoop 
            |> Async.StartImmediate

    let compile =
        Compiler.Compiler.Compile(
            <@ start() @>,
            noReturn = true,
            shouldCompress = true )


module Demo =
    type IndexModule() as x =
        inherit NancyModule()
            do 
                x.Get.["/ping"] <- fun _ -> box (DateTime.Now.ToString( "yyyy/MM/DD HH:mm:ss" ))
                x.Get.["/data"] <- fun _ -> box (FormatterExtensions.AsJson(x.Response, getAssemblyTypes))
                x.Get.["/"] <- fun _ -> box (File.ReadAllText( "../../index.html" ))
                x.Get.["/app.js"] <- fun _ ->
                    let resp = FormatterExtensions.AsText(x.Response, Web.compile)
                    resp.ContentType <- "application/javascript"
                    box resp


[<EntryPoint>]
let main argv = 
    let uri = Uri("http://localhost:6543")
    use host = new NancyHost(uri)
    host.Start()

    Console.WriteLine("Your application is running on " + uri.AbsoluteUri)
    Console.WriteLine("Press any [Enter] to close the host.")
    Console.ReadLine() |> ignore

    0 // return an integer exit code
