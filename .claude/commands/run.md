Run the WebUI development server (GameService + Blazor WebUI).

Execute the following command in the background:

```bash
pwsh -ExecutionPolicy Bypass -File "./webui.ps1" run
```

This starts both the GameService API server and the Blazor WebUI client. The services will be available at:
- GameService: http://localhost:5000
- WebUI: http://localhost:5001
