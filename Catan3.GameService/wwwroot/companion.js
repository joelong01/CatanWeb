/**
 * Catan Companion JavaScript - SignalR Edition
 * Handles real-time communication with game service via SignalR
 */

class CatanCompanion {
    constructor() {
        // Configuration
        this.config = {
            apiBaseUrl: window.location.origin,
            signalRUrl: `${window.location.origin}/gameHub`,
            reconnectDelay: 2000, // 2 seconds between reconnection attempts
            maxReconnectAttempts: 10
        };

        // State
        this.gameId = null; // Will be set when user selects a game
        this.selectedPlayerId = null;
        this.currentGameState = null;
        this.gameVersion = 0;
        this.connectionStatus = 'connecting';
        this.reconnectAttempts = 0;
        
        // SignalR connection
        this.connection = null;
        this.pendingCommands = new Map(); // Track command completion
        
        // Demo mode support
        this.demoMode = window.DEMO_MODE || false;
        this.demoState = window.DEMO_STATE || null;
        
        // UI state for new features
        this.selectedVertex = null;
        this.selectedTileIndex = null;
        this.supplementalTimer = null;
        this.selectedRoll = null;
        this.availableGames = [];
        this.showingGameSelection = true;

        // DOM elements
        this.elements = {
            connectionStatus: document.getElementById('connectionStatus'),
            playerSelect: document.getElementById('playerSelect'),
            playerList: document.getElementById('playerList'),
            currentPlayer: document.getElementById('currentPlayer'),
            waitingFor: document.getElementById('waitingFor'),
            gameStateDisplay: document.getElementById('gameStateDisplay'),
            gameVersion: document.getElementById('gameVersion'),
            gameId: document.getElementById('gameId'),
            lastUpdate: document.getElementById('lastUpdate'),
            nextBtn: document.getElementById('nextBtn'),
            undoBtn: document.getElementById('undoBtn'),
            redoBtn: document.getElementById('redoBtn'),
            // State-specific sections
            stateContent: document.getElementById('stateContent'),
            // Will be created dynamically
            shuffleBtn: null,
            rollButtons: null,
            purchaseButtons: document.getElementById('purchaseButtons'),
            messageContainer: document.getElementById('messageContainer'),
            errorModal: document.getElementById('errorModal'),
            errorMessage: document.getElementById('errorMessage')
        };

        this.init();
    }

    async init() {
        this.updateConnectionStatus('connecting');
        
        // Setup event listeners
        this.setupEventListeners();
        
        if (this.demoMode) {
            this.initDemoMode();
        } else {
            try {
                // Initialize SignalR connection
                await this.initializeSignalR();
                
                // First, check if a gameId was provided in URL
                const urlGameId = this.getGameIdFromUrl();
                if (urlGameId) {
                    this.gameId = urlGameId;
                    this.showingGameSelection = false;
                    await this.connectToGame();
                } else {
                    // Show game selection interface
                    await this.loadAvailableGames();
                    this.showGameSelection();
                }
                
                this.updateConnectionStatus('connected');
                this.showMessage('Connected to game service via SignalR', 'success');
            } catch (error) {
                console.error('Initialization failed:', error);
                this.updateConnectionStatus('error');
                this.showError('Failed to connect to game service. Please ensure the game service is running and accessible.');
            }
        }
    }

    async initializeSignalR() {
        console.log('[COMPANION] Initializing SignalR connection...');
        
        // Create SignalR connection
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(this.config.signalRUrl)
            .withAutomaticReconnect([0, 2000, 10000, 30000]) // Automatic reconnection
            .build();

        // Setup event handlers
        this.setupSignalRHandlers();

        // Start connection
        await this.connection.start();
        console.log('[COMPANION] SignalR connection established');
    }

    setupSignalRHandlers() {
        // Game state updates
        this.connection.on("GameStateUpdated", (gameModel) => {
            console.log('[COMPANION] Received game state update via SignalR:', {
                gameId: gameModel.gameId,
                gameState: gameModel.gameState,
                version: gameModel.version,
                currentPlayerId: gameModel.currentPlayerId
            });
            this.updateGameState(gameModel);
        });

        // Command completion
        this.connection.on("CommandCompleted", (commandId, success, message) => {
            console.log('[COMPANION] Command completed:', { commandId, success, message });
            this.handleCommandCompletion(commandId, success, message);
        });

        // Command failure
        this.connection.on("CommandFailed", (commandId, error) => {
            console.log('[COMPANION] Command failed:', { commandId, error });
            this.handleCommandFailure(commandId, error);
        });

        // Player presence
        this.connection.on("PlayerPresenceChanged", (playerId, isOnline) => {
            console.log('[COMPANION] Player presence changed:', { playerId, isOnline });
            this.updatePlayerPresence(playerId, isOnline);
        });

        // Connection events
        this.connection.onreconnecting(() => {
            console.log('[COMPANION] SignalR reconnecting...');
            this.updateConnectionStatus('connecting');
            this.showMessage('Reconnecting to game service...', 'info');
        });

        this.connection.onreconnected(() => {
            console.log('[COMPANION] SignalR reconnected');
            this.updateConnectionStatus('connected');
            this.showMessage('Reconnected to game service', 'success');
            
            // Rejoin the game if we were in one
            if (this.gameId && this.selectedPlayerId) {
                this.rejoinGame();
            }
        });

        this.connection.onclose(() => {
            console.log('[COMPANION] SignalR connection closed');
            this.updateConnectionStatus('error');
            this.showMessage('Lost connection to game service', 'error');
        });
    }

    async rejoinGame() {
        if (this.gameId && this.selectedPlayerId) {
            try {
                await this.connection.invoke("JoinGame", this.gameId, this.selectedPlayerId);
                console.log('[COMPANION] Rejoined game after reconnection');
            } catch (error) {
                console.error('[COMPANION] Failed to rejoin game:', error);
            }
        }
    }

    handleCommandCompletion(commandId, success, message) {
        const pendingCommand = this.pendingCommands.get(commandId);
        if (pendingCommand) {
            this.pendingCommands.delete(commandId);
            
            if (success) {
                this.showMessage(message || 'Command completed successfully', 'success');
            } else {
                this.showMessage(message || 'Command failed', 'error');
            }
            
            // Hide processing state
            this.hideProcessingState(commandId);
        }
    }

    handleCommandFailure(commandId, error) {
        const pendingCommand = this.pendingCommands.get(commandId);
        if (pendingCommand) {
            this.pendingCommands.delete(commandId);
            this.showMessage(`Command failed: ${error}`, 'error');
            this.hideProcessingState(commandId);
        }
    }

    showProcessingState(commandId, estimatedCompletionMs) {
        this.pendingCommands.set(commandId, { startTime: Date.now(), estimatedCompletionMs });
        this.showMessage('Processing command...', 'info');
    }

    hideProcessingState(commandId) {
        // Could update UI to remove loading indicators if needed
    }

    updatePlayerPresence(playerId, isOnline) {
        // Update UI to show player presence if needed
        console.log(`[COMPANION] Player ${playerId} is now ${isOnline ? 'online' : 'offline'}`);
    }
    
    async connectToGame() {
        if (!this.gameId) {
            throw new Error('No game selected');
        }
        
        this.elements.gameId.textContent = this.gameId;
        
        // Load initial game state
        await this.loadGameState();
        
        // Join the SignalR game group
        if (this.selectedPlayerId) {
            await this.connection.invoke("JoinGame", this.gameId, this.selectedPlayerId);
            console.log('[COMPANION] Joined SignalR game group');
        }
        
        // Hide game selection and show game interface
        this.hideGameSelection();
    }

    getGameIdFromUrl() {
        const urlParams = new URLSearchParams(window.location.search);
        return urlParams.get('gameId') || window.INITIAL_GAME_ID;
    }

    initDemoMode() {
        this.updateConnectionStatus('connected');
        this.showMessage('Demo Mode - UI Preview Only', 'info');
        
        // Create mock game state for demo
        const mockGameState = this.createMockGameState(this.demoState);
        this.updateGameState(mockGameState);
        
        // Add demo header
        this.addDemoHeader();
    }

    addDemoHeader() {
        const header = document.querySelector('header');
        if (header) {
            const demoNotice = document.createElement('div');
            demoNotice.style.cssText = `
                background: linear-gradient(45deg, #ff6b6b, #feca57);
                color: white;
                padding: 0.5rem;
                text-align: center;
                font-weight: bold;
                margin-bottom: 1rem;
                border-radius: 0.5rem;
                box-shadow: 0 2px 4px rgba(0,0,0,0.1);
            `;
            demoNotice.innerHTML = `
                ?? DEMO MODE - UI Preview Only 
                <a href="/demo" style="color: white; text-decoration: underline; margin-left: 1rem;">? Back to Demo Hub</a>
            `;
            header.appendChild(demoNotice);
        }
    }

    createMockGameState(state) {
        const baseState = {
            gameId: 'demo',
            version: 1,
            gameState: state || 'PickingBoard',
            currentPlayerId: 'player1',
            players: [
                { id: 'player1', name: 'Alice' },
                { id: 'player2', name: 'Bob' },
                { id: 'player3', name: 'Charlie' },
                { id: 'player4', name: 'David' }
            ],
            actionFlags: {
                nextEnabled: true,
                undoEnabled: true,
                redoEnabled: false
            },
            availableEntitlements: [
                { entitlement: 'Settlement', enabled: true },
                { entitlement: 'City', enabled: false },
                { entitlement: 'Road', enabled: true },
                { entitlement: 'Soldier', enabled: true }
            ]
        };

        // Set selected player to first player for demo
        this.selectedPlayerId = 'player1';

        return baseState;
    }

    setupEventListeners() {
        // Player selection
        this.elements.playerSelect.addEventListener('change', async (e) => {
            const oldPlayerId = this.selectedPlayerId;
            this.selectedPlayerId = e.target.value;
            
            if (this.selectedPlayerId) {
                this.showMessage(`Selected player: ${e.target.options[e.target.selectedIndex].text}`, 'info');
                
                // If we have a game and connection, join the game group
                if (this.gameId && this.connection && !this.demoMode) {
                    try {
                        // Leave old game group if needed
                        if (oldPlayerId) {
                            await this.connection.invoke("LeaveGame", this.gameId, oldPlayerId);
                        }
                        
                        // Join new game group
                        await this.connection.invoke("JoinGame", this.gameId, this.selectedPlayerId);
                    } catch (error) {
                        console.error('Failed to update SignalR game group:', error);
                    }
                }
            }
            
            this.updateUI();
        });

        // Keyboard shortcuts
        document.addEventListener('keydown', (e) => {
            if (e.key === 'n' && e.ctrlKey) {
                e.preventDefault();
                this.doAction('Next');
            } else if (e.key === 'z' && e.ctrlKey) {
                e.preventDefault();
                this.doAction('Undo');
            } else if (e.key === 'y' && e.ctrlKey) {
                e.preventDefault();
                this.doAction('Redo');
            }
        });
    }

    async loadAvailableGames() {
        try {
            console.log('[COMPANION] Loading available games...');
            
            const response = await fetch(`${this.config.apiBaseUrl}/api/companion/games`);
            
            console.log(`[COMPANION] Available games response - Status: ${response.status} ${response.statusText}`);
            
            if (!response.ok) {
                const errorText = await response.text();
                console.error(`[COMPANION] Failed to load available games - Status: ${response.status}, Response: ${errorText}`);
                throw new Error(`HTTP ${response.status}: ${response.statusText} - ${errorText}`);
            }
            
            const data = await response.json();
            console.log(`[COMPANION] Loaded ${data.games.length} available games:`, data.games);
            
            this.availableGames = data.games;
            
        } catch (error) {
            console.error('[COMPANION] Failed to load available games:', error);
            throw error;
        }
    }
    
    showGameSelection() {
        console.log('[COMPANION] Showing game selection interface');
        
        const container = document.querySelector('main');
        if (!container) return;
        
        container.innerHTML = `
            <div class="game-selection">
                <h2>?? Select a Game</h2>
                ${this.availableGames.length === 0 ? this.createNoGamesMessage() : this.createGamesList()}
                <div class="game-selection-actions">
                    <button id="refreshGamesBtn" class="action-btn secondary">
                        <span class="btn-icon">??</span>
                        <span class="btn-text">Refresh Games</span>
                    </button>
                </div>
            </div>
        `;
        
        // Setup event listeners
        this.setupGameSelectionEvents();
    }
    
    createNoGamesMessage() {
        return `
            <div class="no-games-message">
                <div class="no-games-icon">??</div>
                <h3>No Active Games Found</h3>
                <p>There are currently no games available to join.</p>
                <p>Create a new game using the desktop app or PowerShell script:</p>
                <code>./start-new-game.ps1</code>
            </div>
        `;
    }
    
    createGamesList() {
        return `
            <div class="games-list">
                ${this.availableGames.map(game => this.createGameCard(game)).join('')}
            </div>
        `;
    }
    
    createGameCard(game) {
        const isActiveClass = game.isActive ? 'active' : 'inactive';
        const statusIcon = game.isActive ? '??' : '??';
        
        return `
            <div class="game-card ${isActiveClass}" data-game-id="${game.gameId}">
                <div class="game-card-header">
                    <h3>${game.displayName}</h3>
                    <span class="game-status">${statusIcon} ${game.gameState}</span>
                </div>
                <div class="game-card-info">
                    <div class="game-meta">
                        <span class="game-type">${game.gameType}</span>
                        <span class="game-players">${game.playerCount} players</span>
                        <span class="game-time">${game.createdTimeDisplay}</span>
                    </div>
                    <div class="game-players-list">
                        <strong>Players:</strong> ${game.playerNames.join(', ')}
                    </div>
                    ${game.currentPlayer ? `<div class="current-player">Current: <strong>${game.currentPlayer}</strong></div>` : ''}
                </div>
                <div class="game-card-actions">
                    <button class="join-game-btn action-btn primary" data-game-id="${game.gameId}">
                        <span class="btn-icon">??</span>
                        <span class="btn-text">Join Game</span>
                    </button>
                </div>
            </div>
        `;
    }
    
    setupGameSelectionEvents() {
        // Refresh games button
        const refreshBtn = document.getElementById('refreshGamesBtn');
        if (refreshBtn) {
            refreshBtn.onclick = async () => {
                refreshBtn.disabled = true;
                refreshBtn.querySelector('.btn-text').textContent = 'Refreshing...';
                
                try {
                    await this.loadAvailableGames();
                    this.showGameSelection();
                } catch (error) {
                    this.showError('Failed to refresh games list');
                } finally {
                    refreshBtn.disabled = false;
                    refreshBtn.querySelector('.btn-text').textContent = 'Refresh Games';
                }
            };
        }
        
        // Join game buttons
        const joinButtons = document.querySelectorAll('.join-game-btn');
        joinButtons.forEach(btn => {
            btn.onclick = async () => {
                const gameId = btn.dataset.gameId;
                await this.selectGame(gameId);
            };
        });
    }
    
    async selectGame(gameId) {
        try {
            console.log(`[COMPANION] Selecting game: ${gameId}`);
            
            this.gameId = gameId;
            this.showingGameSelection = false;
            
            this.showMessage(`Connecting to game...`, 'info');
            
            await this.connectToGame();
            
            this.showMessage(`Connected to game successfully!`, 'success');
            
        } catch (error) {
            console.error('[COMPANION] Failed to select game:', error);
            this.showError(`Failed to connect to game: ${error.message}`);
            
            // Reset state
            this.gameId = null;
            this.showingGameSelection = true;
        }
    }
    
    hideGameSelection() {
        this.showingGameSelection = false;
        // The main UI will be populated by updateGameState
    }

    updateConnectionStatus(status) {
        this.connectionStatus = status;
        const statusDot = this.elements.connectionStatus.querySelector('.status-dot');
        const statusText = this.elements.connectionStatus.querySelector('.status-text');
        
        statusDot.className = 'status-dot';
        
        switch (status) {
            case 'connected':
                statusDot.classList.add('connected');
                statusText.textContent = this.demoMode ? 'Demo Mode' : 'SignalR Connected';
                break;
            case 'connecting':
                statusText.textContent = 'Connecting...';
                break;
            case 'error':
                statusDot.classList.add('error');
                statusText.textContent = 'Connection Error';
                break;
        }
    }

    async loadGameState() {
        try {
            console.log(`[COMPANION] Loading game state for gameId: ${this.gameId}`);
            console.log(`[COMPANION] API URL: ${this.config.apiBaseUrl}/api/gamestate/${this.gameId}`);
            
            const response = await fetch(`${this.config.apiBaseUrl}/api/gamestate/${this.gameId}`);
            
            console.log(`[COMPANION] Game state response - Status: ${response.status} ${response.statusText}`);
            console.log(`[COMPANION] Response headers:`, Object.fromEntries(response.headers.entries()));
            
            if (!response.ok) {
                const errorText = await response.text();
                console.error(`[COMPANION] Game state request failed - Status: ${response.status}, Response: ${errorText}`);
                
                if (response.status === 404) {
                    throw new Error(`Game "${this.gameId}" not found. Please create a game first using the desktop app.`);
                }
                throw new Error(`HTTP ${response.status}: ${response.statusText} - ${errorText}`);
            }
            
            const gameState = await response.json();
            console.log(`[COMPANION] Game state loaded successfully:`, {
                gameId: gameState.gameId,
                gameState: gameState.gameState,
                version: gameState.version,
                currentPlayerId: gameState.currentPlayerId,
                playerCount: gameState.players?.length || 0
            });
            
            this.updateGameState(gameState);
        } catch (error) {
            console.error('[COMPANION] Failed to load game state:', error);
            throw error;
        }
    }

    populatePlayerSelect(players) {
        const select = this.elements.playerSelect;
        
        // Clear existing options except the first one
        while (select.children.length > 1) {
            select.removeChild(select.lastChild);
        }
        
        // Add player options from GameModel.players
        if (players && players.length > 0) {
            players.forEach(player => {
                const option = document.createElement('option');
                option.value = player.id;
                option.textContent = player.name || player.id;
                // Auto-select first player in demo mode
                if (this.demoMode && player.id === players[0].id) {
                    option.selected = true;
                    this.selectedPlayerId = player.id;
                }
                select.appendChild(option);
            });
        } else {
            // Add a placeholder if no players are available
            const option = document.createElement('option');
            option.value = '';
            option.textContent = 'No players available - start a game first';
            option.disabled = true;
            select.appendChild(option);
        }
    }

    updatePlayerList(players, currentPlayerId) {
        // Update the player list in header if element exists
        if (this.elements.playerList && players && players.length > 0) {
            const playerElements = players.map(player => {
                const isCurrentPlayer = player.id === currentPlayerId;
                const isSelectedPlayer = player.id === this.selectedPlayerId;
                
                let className = 'player-name';
                if (isCurrentPlayer) className += ' current-player';
                if (isSelectedPlayer) className += ' selected-player';
                
                return `<span class="${className}">${player.name || player.id}</span>`;
            });
            
            this.elements.playerList.innerHTML = playerElements.join(' ');
        } else if (this.elements.playerList) {
            this.elements.playerList.innerHTML = '<span class="player-name">No players</span>';
        }
    }

    updateGameState(gameState) {
        this.currentGameState = gameState;
        this.gameVersion = gameState.version || 0;
        
        // Update player information from GameModel.players (single source of truth)
        if (gameState.players) {
            this.populatePlayerSelect(gameState.players);
            this.updatePlayerList(gameState.players, gameState.currentPlayerId);
        }
        
        // Update UI elements
        const currentPlayer = gameState.players?.find(p => p.id === gameState.currentPlayerId);
        this.elements.currentPlayer.textContent = currentPlayer?.name || gameState.currentPlayerId || '-';
        
        if (this.elements.waitingFor) {
            this.elements.waitingFor.textContent = this.getWaitingForText(gameState.gameState);
        }
        
        this.elements.gameStateDisplay.textContent = this.formatGameState(gameState.gameState);
        this.elements.gameVersion.textContent = this.gameVersion;
        this.elements.lastUpdate.textContent = new Date().toLocaleTimeString();
        
        this.updateActionButtons(gameState);
        this.updateStateSpecificUI(gameState);
        
        // If showing game selection, refresh the available games list
        if (this.showingGameSelection) {
            this.loadAvailableGames();
        }
    }

    getWaitingForText(gameState) {
        const waitingMap = {
            'PickingBoard': 'Board Selection...',
            'WaitingForRollForOrder': 'Roll for Order...',
            'AllocateResourceForward': 'Place Initial Settlements...',
            'AllocateResourceReverse': 'Place Final Settlements...',
            'WaitingForRoll': 'Select Roll...',
            'WaitingForNext': 'Choose Action...',
            'PickSupplementalPlayers': 'Choose Supplemental...',
            'MustMoveRobber': 'Move Robber...'
        };
        return waitingMap[gameState] || 'Game Action...';
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
            'PickSupplementalPlayers': 'Choose Supplemental',
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
        
        // In demo mode, enable some buttons for interaction
        if (this.demoMode) {
            this.elements.nextBtn.disabled = false;
            this.elements.undoBtn.disabled = false;
        }
        
        // Special handling for WaitingForRoll state
        if (gameState.gameState === 'WaitingForRoll' && !this.selectedRoll && !this.demoMode) {
            this.elements.nextBtn.disabled = true; // Always disabled until roll selected
        }
    }

    updateStateSpecificUI(gameState) {
        const stateContent = this.elements.stateContent;
        if (!stateContent) return;
        
        // Clear previous state-specific content
        stateContent.innerHTML = '';
        
        const isCurrentPlayer = this.selectedPlayerId === gameState.currentPlayerId || this.demoMode;
        
        switch (gameState.gameState) {
            case 'PickingBoard':
                this.createPickingBoardUI(stateContent, isCurrentPlayer);
                break;
            case 'AllocateResourceForward':
            case 'AllocateResourceReverse':
                this.createAllocationUI(stateContent, isCurrentPlayer, gameState);
                break;
            case 'PickSupplementalPlayers':
                this.createSupplementalUI(stateContent, isCurrentPlayer);
                break;
            case 'WaitingForRoll':
                this.createRollUI(stateContent, isCurrentPlayer);
                break;
            case 'WaitingForNext':
                this.createPurchaseUI(stateContent, isCurrentPlayer, gameState);
                break;
            case 'MustMoveRobber':
                this.createMoveRobberUI(stateContent, isCurrentPlayer);
                break;
            default:
                // Default state - show current state info
                stateContent.innerHTML = `
                    <div class="state-info">
                        <h3>${this.formatGameState(gameState.gameState)}</h3>
                        <p>Waiting for game action...</p>
                        ${!isCurrentPlayer && this.selectedPlayerId && !this.demoMode ? '<p><em>Waiting for your turn</em></p>' : ''}
                        ${!this.selectedPlayerId && !this.demoMode ? '<p><em>Please select your player above</em></p>' : ''}
                    </div>
                `;
                break;
        }
    }

    createPickingBoardUI(container, isCurrentPlayer) {
        container.innerHTML = `
            <div class="state-section">
                <h3>Board Setup</h3>
                <button id="shuffleBtn" class="action-btn primary" ${!isCurrentPlayer ? 'disabled' : ''}>
                    <span class="btn-icon">??</span>
                    <span class="btn-text">Shuffle Board</span>
                </button>
                ${!isCurrentPlayer && !this.demoMode ? '<p><em>Only the current player can shuffle the board</em></p>' : ''}
                ${this.demoMode ? '<p><em>Demo Mode: Click to see button interaction</em></p>' : ''}
            </div>
        `;
        
        this.elements.shuffleBtn = document.getElementById('shuffleBtn');
        if (isCurrentPlayer) {
            this.elements.shuffleBtn.onclick = () => this.doAction('Shuffle');
        }
    }

    createAllocationUI(container, isCurrentPlayer, gameState) {
        container.innerHTML = `
            <div class="state-section">
                <h3>Settlement Placement</h3>
                <div class="hex-selector">
                    <div class="hex-container">
                        <div class="hex-tile">?</div>
                        <div class="vertex-buttons">
                            <button class="vertex-btn" data-vertex="TopLeft">?</button>
                            <button class="vertex-btn" data-vertex="TopRight">?</button>
                            <button class="vertex-btn" data-vertex="MiddleLeft">?</button>
                            <button class="vertex-btn" data-vertex="MiddleRight">?</button>
                            <button class="vertex-btn" data-vertex="BottomLeft">?</button>
                            <button class="vertex-btn" data-vertex="BottomRight">?</button>
                        </div>
                    </div>
                </div>
                <div class="tile-selector">
                    <label for="tileSelect">Tile:</label>
                    <select id="tileSelect" ${!isCurrentPlayer ? 'disabled' : ''}>
                        <option value="">Select Tile...</option>
                    </select>
                </div>
                <button id="placeSettlementBtn" class="action-btn primary" disabled>
                    <span class="btn-icon catan-icon settlement"></span>
                    <span class="btn-text">Place Settlement</span>
                </button>
                ${!isCurrentPlayer && !this.demoMode ? '<p><em>Waiting for your turn to place settlements</em></p>' : ''}
                ${this.demoMode ? '<p><em>Demo Mode: Click vertex and select tile to see interactions</em></p>' : ''}
            </div>
        `;
        
        if (isCurrentPlayer) {
            // Setup vertex selection
            const vertexButtons = container.querySelectorAll('.vertex-btn');
            vertexButtons.forEach(btn => {
                btn.onclick = () => this.selectVertex(btn.dataset.vertex, vertexButtons);
            });
            
            // Setup tile selection
            const tileSelect = document.getElementById('tileSelect');
            // Populate with available tiles (would come from game state in real implementation)
            for (let i = 1; i <= 19; i++) {
                const option = document.createElement('option');
                option.value = i;
                option.textContent = `Tile ${i}`;
                tileSelect.appendChild(option);
            }
            
            tileSelect.onchange = () => {
                this.selectedTileIndex = tileSelect.value;
                this.updatePlaceSettlementButton();
            };
            
            // Setup place settlement button
            document.getElementById('placeSettlementBtn').onclick = () => this.placeSettlement();
        }
    }

    createSupplementalUI(container, isCurrentPlayer) {
        container.innerHTML = `
            <div class="state-section">
                <h3>Supplemental Building</h3>
                <div class="timer-display">
                    <span id="countdownTimer">?? Choose in 10 seconds...</span>
                </div>
                <div class="supplemental-buttons">
                    <button id="doSupplementalBtn" class="action-btn primary" ${!isCurrentPlayer ? 'disabled' : ''}>
                        <span class="btn-icon">?</span>
                        <span class="btn-text">Do Supplemental</span>
                    </button>
                    <button id="declineSupplementalBtn" class="action-btn secondary" ${!isCurrentPlayer ? 'disabled' : ''}>
                        <span class="btn-icon">?</span>
                        <span class="btn-text">Decline</span>
                    </button>
                </div>
                ${!isCurrentPlayer && !this.demoMode ? '<p><em>Waiting for your turn to make supplemental choice</em></p>' : ''}
                ${this.demoMode ? '<p><em>Demo Mode: Timer runs automatically, buttons are interactive</em></p>' : ''}
            </div>
        `;
        
        if (isCurrentPlayer) {
            document.getElementById('doSupplementalBtn').onclick = () => this.doSupplemental(true);
            document.getElementById('declineSupplementalBtn').onclick = () => this.doSupplemental(false);
            
            // Start countdown timer
            this.startSupplementalTimer();
        }
    }

    createRollUI(container, isCurrentPlayer) {
        container.innerHTML = `
            <div class="state-section">
                <h3>Roll Dice</h3>
                <div class="dice-grid">
                    ${Array.from({length: 11}, (_, i) => i + 2).map(num => 
                        `<button class="dice-btn" data-roll="${num}" ${!isCurrentPlayer ? 'disabled' : ''}>${num}</button>`
                    ).join('')}
                </div>
                <button id="knightBtn" class="action-btn knight" ${!isCurrentPlayer ? 'disabled' : ''}>
                    <span class="btn-icon catan-icon knight"></span>
                    <span class="btn-text">Play Knight</span>
                </button>
                ${!isCurrentPlayer && !this.demoMode ? '<p><em>Waiting for your turn to roll dice</em></p>' : ''}
                ${this.demoMode ? '<p><em>Demo Mode: Click any number to see roll selection</em></p>' : ''}
            </div>
        `;
        
        if (isCurrentPlayer) {
            // Setup dice buttons
            const diceButtons = container.querySelectorAll('.dice-btn');
            diceButtons.forEach(btn => {
                btn.onclick = () => this.selectRoll(parseInt(btn.dataset.roll), diceButtons);
            });
            
            // Setup knight button
            document.getElementById('knightBtn').onclick = () => this.playKnight();
        }
    }

    createPurchaseUI(container, isCurrentPlayer, gameState) {
        const entitlements = gameState.availableEntitlements || [];
        
        container.innerHTML = `
            <div class="state-section">
                <h3>Purchase & Actions</h3>
                <div class="purchase-grid">
                    ${entitlements.map(entitlement => `
                        <button class="purchase-btn ${entitlement.enabled && isCurrentPlayer ? 'available' : ''}" 
                                data-entitlement="${entitlement.entitlement}"
                                ${!entitlement.enabled || !isCurrentPlayer ? 'disabled' : ''}>
                            <div class="purchase-icon catan-icon">${this.getEntitlementIcon(entitlement.entitlement)}</div>
                            <div class="purchase-name">${this.getEntitlementName(entitlement.entitlement)}</div>
                        </button>
                    `).join('')}
                </div>
                ${!isCurrentPlayer && !this.demoMode ? '<p><em>Waiting for your turn to make purchases</em></p>' : ''}
                ${entitlements.length === 0 ? '<p><em>No purchase options available</em></p>' : ''}
                ${this.demoMode ? '<p><em>Demo Mode: Green buttons are "available", gray are disabled</em></p>' : ''}
            </div>
        `;
        
        if (isCurrentPlayer) {
            const purchaseButtons = container.querySelectorAll('.purchase-btn');
            purchaseButtons.forEach(btn => {
                if (!btn.disabled) {
                    btn.onclick = () => this.purchaseEntitlement(btn.dataset.entitlement);
                }
            });
        }
    }

    createMoveRobberUI(container, isCurrentPlayer) {
        container.innerHTML = `
            <div class="state-section">
                <h3>Move Robber</h3>
                <div class="robber-placement">
                    <p>Select a tile to move the robber:</p>
                    <div class="tile-grid">
                        ${Array.from({length: 19}, (_, i) => i + 1).map(num => 
                            `<button class="tile-btn" data-tile="${num}" ${!isCurrentPlayer ? 'disabled' : ''}>Tile ${num}</button>`
                        ).join('')}
                    </div>
                </div>
                <div class="player-target" style="margin-top: 1rem;">
                    <label for="targetPlayerSelect">Target Player (optional):</label>
                    <select id="targetPlayerSelect" ${!isCurrentPlayer ? 'disabled' : ''}>
                        <option value="">No target</option>
                        <option value="player2">Bob</option>
                        <option value="player3">Charlie</option>
                        <option value="player4">David</option>
                    </select>
                </div>
                <button id="moveRobberBtn" class="action-btn primary" disabled>
                    <span class="btn-icon catan-icon pirate"></span>
                    <span class="btn-text">Move Robber</span>
                </button>
                ${this.demoMode ? '<p><em>Demo Mode: Select a tile then click Move Robber</em></p>' : ''}
            </div>
        `;
        
        if (isCurrentPlayer) {
            // Setup tile selection
            const tileButtons = container.querySelectorAll('.tile-btn');
            tileButtons.forEach(btn => {
                btn.onclick = () => this.selectRobberTile(btn.dataset.tile, tileButtons);
            });
            
            // Setup move robber button
            document.getElementById('moveRobberBtn').onclick = () => this.moveRobber();
        }
    }

    selectVertex(vertex, vertexButtons) {
        // Clear previous selection
        vertexButtons.forEach(btn => btn.classList.remove('selected'));
        
        // Select new vertex
        const selectedBtn = Array.from(vertexButtons).find(btn => btn.dataset.vertex === vertex);
        selectedBtn.classList.add('selected');
        
        this.selectedVertex = vertex;
        this.updatePlaceSettlementButton();
        
        if (this.demoMode) {
            this.showMessage(`Selected vertex: ${vertex}`, 'info');
        }
    }

    selectRobberTile(tileId, tileButtons) {
        // Clear previous selection
        tileButtons.forEach(btn => btn.classList.remove('selected'));
        
        // Select new tile
        const selectedBtn = Array.from(tileButtons).find(btn => btn.dataset.tile === tileId);
        selectedBtn.classList.add('selected');
        
        this.selectedRobberTile = tileId;
        
        // Enable move robber button
        const moveBtn = document.getElementById('moveRobberBtn');
        if (moveBtn) {
            moveBtn.disabled = false;
        }
        
        if (this.demoMode) {
            this.showMessage(`Selected tile ${tileId} for robber placement`, 'info');
        }
    }

    updatePlaceSettlementButton() {
        const btn = document.getElementById('placeSettlementBtn');
        if (btn) {
            btn.disabled = !this.selectedVertex || !this.selectedTileIndex;
        }
    }

    async placeSettlement() {
        if (!this.selectedVertex || !this.selectedTileIndex) {
            this.showMessage('Please select both a vertex and tile', 'error');
            return;
        }
        
        if (this.demoMode) {
            this.showMessage(`Demo: Would place settlement at ${this.selectedVertex} on tile ${this.selectedTileIndex}`, 'success');
            this.selectedVertex = null;
            this.selectedTileIndex = null;
            this.updatePlaceSettlementButton();
            return;
        }
        
        try {
            // Create building key for API
            const buildingKey = {
                hexCoordinates: { q: 0, r: 0, s: 0 }, // Would be calculated from tile index
                position: this.selectedVertex
            };
            
            const response = await this.sendGameAction('BuildingUpgradeMessage', { buildingKey });
            if (response.success) {
                this.selectedVertex = null;
                this.selectedTileIndex = null;
            }
        } catch (error) {
            console.error('Failed to place settlement:', error);
        }
    }

    async moveRobber() {
        if (!this.selectedRobberTile) {
            this.showMessage('Please select a tile for the robber', 'error');
            return;
        }
        
        if (this.demoMode) {
            const targetSelect = document.getElementById('targetPlayerSelect');
            const target = targetSelect ? targetSelect.value : '';
            this.showMessage(`Demo: Would move robber to tile ${this.selectedRobberTile}${target ? ` and target ${target}` : ''}`, 'success');
            this.selectedRobberTile = null;
            return;
        }
        
        try {
            const targetSelect = document.getElementById('targetPlayerSelect');
            const targetPlayerId = targetSelect ? targetSelect.value : null;
            
            const response = await this.sendGameAction('MoveRobberMessage', { 
                coordinates: { q: 0, r: 0, s: 0 }, // Would be calculated from tile
                targetPlayerId 
            });
            
            if (response.success) {
                this.selectedRobberTile = null;
            }
        } catch (error) {
            console.error('Failed to move robber:', error);
        }
    }

    startSupplementalTimer() {
        let timeLeft = 10;
        const timerElement = document.getElementById('countdownTimer');
        
        this.supplementalTimer = setInterval(() => {
            timeLeft--;
            if (timerElement) {
                timerElement.textContent = `?? Choose in ${timeLeft} seconds...`;
            }
            
            if (timeLeft <= 0) {
                if (this.demoMode) {
                    this.showMessage('Demo: Timer expired - would auto-decline', 'info');
                } else {
                    this.doSupplemental(false); // Auto-decline
                }
                clearInterval(this.supplementalTimer);
            }
        }, 1000);
    }

    async doSupplemental(doSupplemental) {
        if (this.supplementalTimer) {
            clearInterval(this.supplementalTimer);
        }
        
        if (this.demoMode) {
            const action = doSupplemental ? 'accepted' : 'declined';
            this.showMessage(`Demo: Supplemental ${action}`, 'success');
            return;
        }
        
        try {
            const playerIds = doSupplemental ? [this.selectedPlayerId] : [];
            const response = await this.sendGameAction('PlayersDoingSupplemental', { playerIds });
        } catch (error) {
            console.error('Failed to set supplemental choice:', error);
        }
    }

    selectRoll(rollValue, diceButtons) {
        // Clear previous selection
        diceButtons.forEach(btn => btn.classList.remove('selected'));
        
        // Select new roll
        const selectedBtn = Array.from(diceButtons).find(btn => parseInt(btn.dataset.roll) === rollValue);
        selectedBtn.classList.add('selected');
        
        this.selectedRoll = rollValue;
        
        if (this.demoMode) {
            this.showMessage(`Demo: Selected roll ${rollValue}`, 'success');
        } else {
            // Make the roll
            this.makeRoll(rollValue);
        }
    }

    async makeRoll(rollValue) {
        try {
            // Calculate individual dice that sum to rollValue
            let die1, die2;
            if (rollValue <= 7) {
                die1 = Math.min(rollValue - 1, 6);
                die2 = rollValue - die1;
            } else {
                die1 = Math.max(rollValue - 6, 1);
                die2 = rollValue - die1;
            }
            
            const rollData = {
                normalRoll: rollValue.toString(),
                specialDice: 'None'
            };

            const response = await this.sendGameAction('RollMessage', { roll: rollData });
            if (response.success) {
                this.selectedRoll = null;
            }
        } catch (error) {
            console.error('Failed to roll dice:', error);
        }
    }

    async playKnight() {
        if (this.demoMode) {
            this.showMessage('Demo: Knight played - would transition to Move Robber state', 'success');
            return;
        }
        
        try {
            await this.sendGameAction('PurchaseMessage', { entitlement: 'Soldier' });
        } catch (error) {
            console.error('Failed to play knight:', error);
        }
    }

    getEntitlementIcon(entitlement) {
        // Using Catan font Unicode characters from CatanFont.cs
        const icons = {
            'Settlement': '\uE926',  // Catan.Settlement
            'City': '\uE900',        // Catan.City
            'Road': '\uE909',        // Catan.Road
            'Soldier': '\uE90E',     // Catan.Soldier
            'Knight': '\uE930',      // Catan.Knight
            'BuyKnight': '\uE930',   // Catan.Knight
            'UpgradeKnight': '\uE930', // Catan.Knight
            'Wall': '\uE903',        // Catan.Gate
            'Bishop': '\uE906',      // Catan.Inventor (closest match)
            'Inventor': '\uE906',    // Catan.Inventor
            'Merchant': '\uE908',    // Catan.Merchant
            'Diplomat': '\uE902'     // Catan.Diplomat
        };
        return icons[entitlement] || '?';
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
        if (!this.selectedPlayerId && !this.demoMode) {
            this.showMessage('Please select your player first', 'error');
            return;
        }

        if (this.demoMode) {
            this.showMessage(`Demo: ${action} button clicked`, 'success');
            return;
        }

        try {
            await this.sendGameAction('DoAction', { action });
        } catch (error) {
            console.error(`Failed to ${action}:`, error);
        }
    }

    async purchaseEntitlement(entitlement) {
        if (!this.selectedPlayerId && !this.demoMode) {
            this.showMessage('Please select your player first', 'error');
            return;
        }

        if (this.demoMode) {
            this.showMessage(`Demo: Would purchase ${this.getEntitlementName(entitlement)}`, 'success');
            return;
        }

        try {
            await this.sendGameAction('PurchaseMessage', { entitlement });
        } catch (error) {
            console.error(`Failed to purchase ${entitlement}:`, error);
        }
    }

    async sendGameAction(messageType, messageData) {
        const commandId = this.generateCommandId();
        
        const payload = {
            gameId: this.gameId,
            playerId: this.selectedPlayerId,
            messageType: messageType,
            messageData: messageData,
            timestamp: new Date().toISOString()
        };

        console.log(`[COMPANION] Sending async game action:`, {
            url: `${this.config.apiBaseUrl}/api/game/action`,
            messageType: messageType,
            commandId: commandId,
            gameId: this.gameId,
            playerId: this.selectedPlayerId,
            messageData: messageData
        });

        const startTime = Date.now();
        const response = await fetch(`${this.config.apiBaseUrl}/api/game/action`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(payload)
        });

        const responseTime = Date.now() - startTime;
        console.log(`[COMPANION] Async game action response - Status: ${response.status}, Time: ${responseTime}ms`);

        if (!response.ok) {
            const errorText = await response.text();
            console.error(`[COMPANION] Game action failed - Status: ${response.status}, Response: ${errorText}`);
            throw new Error(`HTTP ${response.status}: ${response.statusText} - ${errorText}`);
        }

        const result = await response.json();
        console.log(`[COMPANION] Game action result:`, {
            success: result.success,
            commandId: result.commandId,
            message: result.message,
            estimatedCompletionMs: result.estimatedCompletionMs,
            responseTimeMs: responseTime
        });

        // Show processing state and wait for completion via SignalR
        if (result.success && result.commandId) {
            this.showProcessingState(result.commandId, result.estimatedCompletionMs);
        }

        return result;
    }

    generateCommandId() {
        return 'cmd_' + Math.random().toString(36).substr(2, 9);
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
            this.updateStateSpecificUI(this.currentGameState);
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