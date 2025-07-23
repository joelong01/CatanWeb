/**
 * Catan Companion JavaScript
 * Handles real-time communication with game service
 */

class CatanCompanion {
    constructor() {
        // Configuration
        this.config = {
            apiBaseUrl: window.location.origin,
            updateInterval: 100, // ms between update attempts
            timeoutDuration: 900000, // 15 minutes (900,000 ms) for local games where players might think
            maxRetries: 5,
            retryDelay: 1000 // 1 second
        };

        // State
        this.gameId = this.getGameIdFromUrl() || 'default';
        this.selectedPlayerId = null;
        this.currentGameState = null;
        this.gameVersion = 0;
        this.isListening = false;
        this.retryCount = 0;
        this.connectionStatus = 'connecting';

        // DOM elements
        this.elements = {
            connectionStatus: document.getElementById('connectionStatus'),
            playerSelect: document.getElementById('playerSelect'),
            currentPlayer: document.getElementById('currentPlayer'),
            gameStateDisplay: document.getElementById('gameStateDisplay'),
            gameVersion: document.getElementById('gameVersion'),
            gameId: document.getElementById('gameId'),
            lastUpdate: document.getElementById('lastUpdate'),
            nextBtn: document.getElementById('nextBtn'),
            undoBtn: document.getElementById('undoBtn'),
            redoBtn: document.getElementById('redoBtn'),
            rollBtn: document.getElementById('rollBtn'),
            rollSection: document.getElementById('rollSection'),
            purchaseButtons: document.getElementById('purchaseButtons'),
            messageContainer: document.getElementById('messageContainer'),
            errorModal: document.getElementById('errorModal'),
            errorMessage: document.getElementById('errorMessage')
        };

        this.init();
    }

    async init() {
        this.updateConnectionStatus('connecting');
        this.elements.gameId.textContent = this.gameId;
        
        // Setup event listeners
        this.setupEventListeners();
        
        try {
            // Load initial game state
            await this.loadGameState();
            await this.loadPlayers();
            
            // Start listening for updates
            this.startListening();
            
            this.updateConnectionStatus('connected');
            this.showMessage('Connected to game service', 'success');
        } catch (error) {
            console.error('Initialization failed:', error);
            this.updateConnectionStatus('error');
            this.showError('Failed to connect to game service');
        }
    }

    setupEventListeners() {
        // Player selection
        this.elements.playerSelect.addEventListener('change', (e) => {
            this.selectedPlayerId = e.target.value;
            this.updateUI();
            if (this.selectedPlayerId) {
                this.showMessage(`Selected player: ${e.target.options[e.target.selectedIndex].text}`, 'info');
            }
        });

        // Keyboard shortcuts
        document.addEventListener('keydown', (e) => {
            if (e.key === 'n' && e.ctrlKey) {
                e.preventDefault();
                this.doAction('Next');
            } else if (e.key === 'z' && e.ctrlKey) {
                e.preventDefault();
                this.doAction('Undo');
            }
        });

        // Handle visibility change (page focus/blur)
        document.addEventListener('visibilitychange', () => {
            if (document.visibilityState === 'visible' && !this.isListening) {
                this.startListening();
            }
        });
    }

    getGameIdFromUrl() {
        const urlParams = new URLSearchParams(window.location.search);
        return urlParams.get('gameId');
    }

    updateConnectionStatus(status) {
        this.connectionStatus = status;
        const statusDot = this.elements.connectionStatus.querySelector('.status-dot');
        const statusText = this.elements.connectionStatus.querySelector('.status-text');
        
        statusDot.className = 'status-dot';
        
        switch (status) {
            case 'connected':
                statusDot.classList.add('connected');
                statusText.textContent = 'Connected';
                break;
            case 'connecting':
                statusText.textContent = 'Connecting...';
                break;
            case 'error':
                statusDot.classList.add('error');
                statusText.textContent = 'Connection Error';
                break;
            case 'listening':
                statusDot.classList.add('connected');
                statusText.textContent = 'Live Updates';
                break;
        }
    }

    async loadPlayers() {
        try {
            const response = await fetch(`${this.config.apiBaseUrl}/api/players/${this.gameId}`);
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            
            const data = await response.json();
            this.populatePlayerSelect(data.players);
        } catch (error) {
            console.error('Failed to load players:', error);
            this.showMessage('Failed to load player list', 'error');
        }
    }

    populatePlayerSelect(players) {
        const select = this.elements.playerSelect;
        
        // Clear existing options except the first one
        while (select.children.length > 1) {
            select.removeChild(select.lastChild);
        }
        
        // Add player options
        players.forEach(player => {
            const option = document.createElement('option');
            option.value = player.id;
            option.textContent = player.name;
            if (player.isCurrentPlayer) {
                option.textContent += ' (Current)';
            }
            select.appendChild(option);
        });
    }

    async loadGameState() {
        try {
            const response = await fetch(`${this.config.apiBaseUrl}/api/gamestate/${this.gameId}`);
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            
            const gameState = await response.json();
            this.updateGameState(gameState);
        } catch (error) {
            console.error('Failed to load game state:', error);
            throw error;
        }
    }

    async startListening() {
        if (this.isListening) return;
        
        this.isListening = true;
        this.updateConnectionStatus('listening');
        
        while (this.isListening) {
            try {
                await this.listenForUpdates();
                this.retryCount = 0; // Reset retry count on success
            } catch (error) {
                console.error('Listen error:', error);
                this.retryCount++;
                
                if (this.retryCount >= this.config.maxRetries) {
                    this.updateConnectionStatus('error');
                    this.showError('Lost connection to game service');
                    this.isListening = false;
                    break;
                }
                
                // Exponential backoff
                const delay = this.config.retryDelay * Math.pow(2, this.retryCount - 1);
                await this.delay(delay);
            }
        }
    }

    async listenForUpdates() {
        const url = `${this.config.apiBaseUrl}/api/gamestate/${this.gameId}/listen?version=${this.gameVersion}&playerId=${this.selectedPlayerId || ''}`;
        
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), this.config.timeoutDuration);
        
        try {
            const response = await fetch(url, {
                signal: controller.signal,
                headers: {
                    'Accept': 'application/json'
                }
            });
            
            clearTimeout(timeoutId);
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }
            
            const gameState = await response.json();
            this.updateGameState(gameState);
            
            // Small delay before next request
            await this.delay(this.config.updateInterval);
        } catch (error) {
            clearTimeout(timeoutId);
            if (error.name === 'AbortError') {
                // Timeout - this is normal, just continue
                return;
            }
            throw error;
        }
    }

    updateGameState(gameState) {
        this.currentGameState = gameState;
        this.gameVersion = gameState.version || 0;
        
        // Update UI elements
        this.elements.currentPlayer.textContent = gameState.currentPlayerName || gameState.currentPlayerId || '-';
        this.elements.gameStateDisplay.textContent = this.formatGameState(gameState.gameState);
        this.elements.gameVersion.textContent = this.gameVersion;
        this.elements.lastUpdate.textContent = new Date().toLocaleTimeString();
        
        this.updateActionButtons(gameState);
        this.updatePurchaseButtons(gameState);
    }

    formatGameState(gameState) {
        const stateMap = {
            'WaitingForNewGame': 'Waiting for Game',
            'PickingBoard': 'Selecting Board',
            'WaitingForRollForOrder': 'Rolling for Order',
            'AllocateResourceForward': 'Initial Setup ?',
            'AllocateResourceReverse': 'Initial Setup ?',
            'WaitingForRoll': 'Waiting for Roll',
            'WaitingForNext': 'Waiting for Next',
            'Supplemental': 'Supplemental Phase',
            'MustMoveRobber': 'Must Move Robber'
        };
        return stateMap[gameState] || gameState;
    }

    updateActionButtons(gameState) {
        const actionFlags = gameState.actionFlags || {};
        const isCurrentPlayer = this.selectedPlayerId === gameState.currentPlayerId;
        
        // Basic action buttons
        this.elements.nextBtn.disabled = !actionFlags.nextEnabled || !isCurrentPlayer;
        this.elements.undoBtn.disabled = !actionFlags.undoEnabled || !isCurrentPlayer;
        this.elements.redoBtn.disabled = !actionFlags.redoEnabled || !isCurrentPlayer;
        
        // Roll button
        const rollEnabled = actionFlags.rollsEnabled && isCurrentPlayer;
        this.elements.rollBtn.disabled = !rollEnabled;
        this.elements.rollSection.style.display = rollEnabled ? 'block' : 'none';
    }

    updatePurchaseButtons(gameState) {
        const container = this.elements.purchaseButtons;
        container.innerHTML = '';
        
        const entitlements = gameState.availableEntitlements || [];
        const isCurrentPlayer = this.selectedPlayerId === gameState.currentPlayerId;
        
        entitlements.forEach(entitlement => {
            const button = document.createElement('button');
            button.className = `purchase-btn ${entitlement.enabled && isCurrentPlayer ? 'available' : ''}`;
            button.disabled = !entitlement.enabled || !isCurrentPlayer;
            button.onclick = () => this.purchaseEntitlement(entitlement.entitlement);
            
            const icon = this.getEntitlementIcon(entitlement.entitlement);
            const name = this.getEntitlementName(entitlement.entitlement);
            
            button.innerHTML = `
                <div class="purchase-icon">${icon}</div>
                <div class="purchase-name">${name}</div>
            `;
            
            container.appendChild(button);
        });
    }

    getEntitlementIcon(entitlement) {
        const icons = {
            'Settlement': '??',
            'City': '??',
            'Road': '???',
            'Soldier': '??',
            'BuyKnight': '???',
            'UpgradeKnight': '?????',
            'Wall': '??',
            'Bishop': '?',
            'Inventor': '??',
            'Merchant': '??',
            'Diplomat': '??'
        };
        return icons[entitlement] || '??';
    }

    getEntitlementName(entitlement) {
        const names = {
            'Settlement': 'Settlement',
            'City': 'City',
            'Road': 'Road',
            'Soldier': 'Knight',
            'BuyKnight': 'Buy Knight',
            'UpgradeKnight': 'Upgrade Knight',
            'Wall': 'Wall',
            'Bishop': 'Bishop',
            'Inventor': 'Inventor',
            'Merchant': 'Merchant',
            'Diplomat': 'Diplomat'
        };
        return names[entitlement] || entitlement;
    }

    async doAction(action) {
        if (!this.selectedPlayerId) {
            this.showMessage('Please select your player first', 'error');
            return;
        }

        try {
            const response = await this.sendGameAction('DoAction', { action });
            if (response.success) {
                this.showMessage(`${action} completed`, 'success');
            } else {
                this.showMessage(response.message || `Failed to ${action.toLowerCase()}`, 'error');
            }
        } catch (error) {
            console.error(`Failed to ${action}:`, error);
            this.showMessage(`Failed to ${action.toLowerCase()}`, 'error');
        }
    }

    async purchaseEntitlement(entitlement) {
        if (!this.selectedPlayerId) {
            this.showMessage('Please select your player first', 'error');
            return;
        }

        try {
            const response = await this.sendGameAction('PurchaseMessage', { entitlement });
            if (response.success) {
                this.showMessage(`Purchased ${this.getEntitlementName(entitlement)}`, 'success');
            } else {
                this.showMessage(response.message || `Failed to purchase ${entitlement}`, 'error');
            }
        } catch (error) {
            console.error(`Failed to purchase ${entitlement}:`, error);
            this.showMessage(`Failed to purchase ${entitlement}`, 'error');
        }
    }

    async rollDice() {
        if (!this.selectedPlayerId) {
            this.showMessage('Please select your player first', 'error');
            return;
        }

        // Generate dice roll (2-12)
        const die1 = Math.floor(Math.random() * 6) + 1;
        const die2 = Math.floor(Math.random() * 6) + 1;
        const total = die1 + die2;

        try {
            const rollData = {
                normalRoll: total,
                redDie: 'None', // Simplified for now
                eventDie: 'None'
            };

            const response = await this.sendGameAction('RollMessage', { roll: rollData });
            if (response.success) {
                this.showMessage(`Rolled ${die1} + ${die2} = ${total}`, 'success');
            } else {
                this.showMessage(response.message || 'Failed to roll dice', 'error');
            }
        } catch (error) {
            console.error('Failed to roll dice:', error);
            this.showMessage('Failed to roll dice', 'error');
        }
    }

    async sendGameAction(messageType, messageData) {
        const payload = {
            gameId: this.gameId,
            playerId: this.selectedPlayerId,
            messageType: messageType,
            messageData: messageData,
            timestamp: new Date().toISOString()
        };

        const response = await fetch(`${this.config.apiBaseUrl}/api/game/action`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
        }

        return await response.json();
    }

    showMessage(text, type = 'info') {
        const message = document.createElement('div');
        message.className = `message ${type}`;
        message.textContent = text;
        
        this.elements.messageContainer.appendChild(message);
        
        // Auto-remove after 5 seconds
        setTimeout(() => {
            if (message.parentNode) {
                message.parentNode.removeChild(message);
            }
        }, 5000);
        
        // Scroll to bottom
        this.elements.messageContainer.scrollTop = this.elements.messageContainer.scrollHeight;
    }

    showError(message) {
        this.elements.errorMessage.textContent = message;
        this.elements.errorModal.classList.add('show');
    }

    updateUI() {
        if (this.currentGameState) {
            this.updateActionButtons(this.currentGameState);
            this.updatePurchaseButtons(this.currentGameState);
        }
    }

    delay(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }
}

// Global functions for HTML onclick handlers
window.doAction = function(action) {
    if (window.companion) {
        window.companion.doAction(action);
    }
};

window.rollDice = function() {
    if (window.companion) {
        window.companion.rollDice();
    }
};

window.closeErrorModal = function() {
    const modal = document.getElementById('errorModal');
    modal.classList.remove('show');
};

// Initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    window.companion = new CatanCompanion();
});

// Service Worker registration for PWA (future enhancement)
if ('serviceWorker' in navigator) {
    window.addEventListener('load', function() {
        // Will be implemented later for offline support
    });
}