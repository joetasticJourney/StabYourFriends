export class Connection {
    constructor() {
        this.ws = null;
        this.isConnected = false;
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 3;

        this.onConnected = null;
        this.onDisconnected = null;
        this.onMessage = null;
        this.onError = null;
    }

    connect(ip, port) {
        return new Promise((resolve, reject) => {
            if (this.ws && this.ws.readyState === WebSocket.OPEN) {
                resolve();
                return;
            }

            const url = `ws://${ip}:${port}`;
            console.log('Attempting to connect to:', url);

            try {
                this.ws = new WebSocket(url);
                console.log('WebSocket created, waiting for connection...');
            } catch (e) {
                console.error('Failed to create WebSocket:', e);
                reject(new Error('Invalid connection URL'));
                return;
            }

            const timeout = setTimeout(() => {
                console.log('Connection timeout, readyState:', this.ws.readyState);
                if (this.ws.readyState !== WebSocket.OPEN) {
                    this.ws.close();
                    reject(new Error('Connection timed out'));
                }
            }, 5000);

            this.ws.onopen = () => {
                console.log('WebSocket connected!');
                clearTimeout(timeout);
                this.isConnected = true;
                this.reconnectAttempts = 0;
                if (this.onConnected) this.onConnected();
                resolve();
            };

            this.ws.onclose = (event) => {
                console.log('WebSocket closed:', event.code, event.reason);
                clearTimeout(timeout);
                this.isConnected = false;
                if (this.onDisconnected) this.onDisconnected(event.code, event.reason);
            };

            this.ws.onerror = (error) => {
                console.error('WebSocket error:', error);
                clearTimeout(timeout);
                if (this.onError) this.onError(error);
                reject(new Error('Connection failed'));
            };

            this.ws.onmessage = (event) => {
                console.log('Received message:', event.data);
                try {
                    const message = JSON.parse(event.data);
                    if (this.onMessage) this.onMessage(message);
                } catch (e) {
                    console.error('Failed to parse message:', e);
                }
            };
        });
    }

    disconnect() {
        if (this.ws) {
            this.ws.close();
            this.ws = null;
        }
        this.isConnected = false;
    }

    send(message) {
        if (this.ws && this.ws.readyState === WebSocket.OPEN) {
            console.log('Sending message:', message);
            this.ws.send(JSON.stringify(message));
        } else {
            console.warn('Cannot send, WebSocket not open. State:', this.ws?.readyState);
        }
    }

    sendJoin(playerName) {
        this.send({
            type: 'join',
            playerName: playerName
        });
    }

    sendReady(isReady) {
        this.send({
            type: 'ready',
            isReady: isReady
        });
    }

    sendInput(moveX, moveY, action1, action2) {
        this.send({
            type: 'input',
            moveX: moveX,
            moveY: moveY,
            action1: action1,
            action2: action2
        });
    }

    sendShake() {
        this.send({
            type: 'shake'
        });
    }
}
