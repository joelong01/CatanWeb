# Catan3 Companion SSL/HTTPS Troubleshooting Guide

## ?? **ERR_SSL_PROTOCOL_ERROR** - Common Issue & Solutions

### **Problem:**

Browser shows "The connection for this site is not secure" or "ERR_SSL_PROTOCOL_ERROR" when accessing the companion.

### **Root Cause:**

The Catan3 Game Service runs on **HTTP** (port 8080), but your browser is trying to access it via **HTTPS**.

---

## ? **Solutions (Try in Order):**

### **1. Use Correct HTTP URL** ? (Most Common Fix)

**? Correct**: `http://localhost:8080/companion`  
**? Wrong**: `https://localhost:8080/companion`

Make sure you're typing `http://` (not `https://`) in your browser address bar.

### **2. Use Our Helper Scripts**

```bash
# Open with correct URL automatically
.\open-companion.ps1    # PowerShell
.\open-companion.bat    # Batch file
```

### **3. Clear Browser HSTS Cache**

If your browser automatically redirects HTTP to HTTPS:

#### **Chrome/Edge:**

1. Go to: `chrome://net-internals/#hsts` (or `edge://net-internals/#hsts`)
2. Scroll to "Delete domain security policies"
3. Enter: `localhost`
4. Click: "Delete"
5. Try again: `http://localhost:8080/companion`

#### **Firefox:**

1. Go to: Settings ? Privacy & Security
2. Click: "Clear Data..." under "Cookies and Site Data"
3. Check both boxes and click "Clear"
4. Try again: `http://localhost:8080/companion`

### **4. Use Incognito/Private Mode**

Try accessing `http://localhost:8080/companion` in:

- **Chrome**: Ctrl+Shift+N
- **Firefox**: Ctrl+Shift+P  
- **Edge**: Ctrl+Shift+N

### **5. Different Browser**

If one browser has cached HTTPS redirects, try a different browser.

### **6. Check Game Service Status**

Ensure the service is running properly:

```bash
# Check if service is running
.\check-diagram-status.ps1

# Start the service if not running
.\run-game-service.ps1
```

---

## ?? **Verification Steps:**

### **1. Check Service is Running**

You should see this output when starting the service:

```text
?? Catan3 Game Service Starting - SignalR Enabled
?? MOBILE COMPANION URLS:
  ?? Local:   http://localhost:8080/companion
  ?? Network: http://[your-ip]:8080/companion
```

### **2. Test API Directly**

Open in browser: `http://localhost:8080/api/companion/games`

- Should show JSON response with available games
- If this works, the service is running correctly

### **3. Test Static Files**

Open in browser: `http://localhost:8080/companion.css`

- Should show CSS content
- Confirms static file serving is working

---

## ?? **Alternative Access Methods:**

### **Network Access (Other Devices)**

If accessing from another device on your network:

```text
http://[your-computer-ip]:8080/companion
```

Replace `[your-computer-ip]` with your actual IP address (shown in service startup log).

### **Demo Mode (No Game Required)**

Test the interface without a game:

```text
http://localhost:8080/demo
http://localhost:8080/companion/demo/WaitingForRoll
```

---

## ??? **Advanced Troubleshooting:**

### **Check Port Conflicts**

If service won't start:

```bash
# Check what's using port 8080
netstat -an | findstr :8080
```

### **Firewall Issues**

If accessing from network devices:

1. Check Windows Firewall settings
2. Allow "Catan3.GameService.exe" through firewall
3. Allow port 8080 inbound connections

### **DNS/Hosts File Issues**

If `localhost` doesn't work:

- Try: `http://127.0.0.1:8080/companion`
- Check: `C:\Windows\System32\drivers\etc\hosts`

---

## ?? **Why No HTTPS?**

The Catan3 Game Service is designed for local network use and doesn't require SSL certificates. HTTPS is disabled in development mode to:

- Avoid certificate complexity
- Enable easy local network access
- Simplify development and testing

This is normal and secure for local network gaming!

---

## ?? **Still Having Issues?**

If none of these solutions work:

1. **Restart the service**:

   ```bash
   # Stop service (Ctrl+C)
   # Then restart:
   .\run-game-service.ps1
   ```

2. **Check Windows hosts file**: Ensure localhost points to 127.0.0.1

3. **Try different port**: Modify `Program.cs` to use port 8081 if 8080 conflicts

4. **Browser developer tools**: Press F12 and check Console/Network tabs for error details
