export class Controller {
    constructor() {
        this.touchpad = null;
        this.touchpadIndicator = null;
        this.action1Btn = null;
        this.action2Btn = null;

        this.moveX = 0;
        this.moveY = 0;
        this.action1 = false;
        this.action2 = false;

        this.touchpadActive = false;
        this.touchpadTouchId = null;
        this.touchpadRect = null;

        this.onInputChange = null;
        this.inputSendInterval = null;
        this.inputSendRate = 1000 / 30; // 30Hz

        // Shake detection
        this.onShake = null;
        this.shakeThreshold = 15; // Acceleration threshold for shake detection
        this.shakeCooldown = 350; // Minimum ms between shake events
        this.lastShakeTime = 0;
        this.lastAcceleration = { x: 0, y: 0, z: 0 };
        this.motionListenerActive = false;
        this.debugMotion = false; // Set to true to log motion values

        // Web Audio API for low-latency sound
        this.audioContext = null;
        this.gotchaBuffer = null;
        this.stabBuffer = null;

        this.controllerMode = false;

        // Stab mode (activated when grappling)
        this.stabMode = true;
        this.stabSpeed = 1;
        this.orientAlpha = 0;

        // Motion/orientation enable flags (controlled by stab mode setting)
        this.motionEnabled = true;
        this.orientationEnabled = true;

        // Button cooldowns
        this.buttonCooldown = 200; // ms
        this.lastAction1Time = 0;
        this.lastStabTime = 0;
    }

    init() {
        this.touchpad = document.getElementById('touchpad');
        this.touchpadIndicator = document.getElementById('touchpad-indicator');
        this.action1Btn = document.getElementById('action1-btn');
        this.action2Btn = document.getElementById('action2-btn');
        this.stabBtn = document.getElementById('stab-btn-new');

        this.setupTouchpad();
        this.setupButtons();
        this.setupStabButton();
        this.setupShakeDetection();
        this.setupOrientationTracking();
    }

    setupTouchpad() {
        // Touch events
        this.touchpad.addEventListener('touchstart', (e) => this.onTouchpadStart(e), { passive: false });
        this.touchpad.addEventListener('touchmove', (e) => this.onTouchpadMove(e), { passive: false });
        this.touchpad.addEventListener('touchend', (e) => this.onTouchpadEnd(e), { passive: false });
        this.touchpad.addEventListener('touchcancel', (e) => this.onTouchpadEnd(e), { passive: false });

        // Mouse events for desktop testing
        this.touchpad.addEventListener('mousedown', (e) => this.onMouseDown(e));
        window.addEventListener('mousemove', (e) => this.onMouseMove(e));
        window.addEventListener('mouseup', (e) => this.onMouseUp(e));
    }

    onTouchpadStart(e) {
        e.preventDefault();
        if (this.touchpadActive) return;

        const touch = e.changedTouches[0];
        this.touchpadTouchId = touch.identifier;
        this.touchpadActive = true;
        this.touchpadRect = this.touchpad.getBoundingClientRect();

        this.updateTouchpadPosition(touch.clientX, touch.clientY);
    }

    onTouchpadMove(e) {
        e.preventDefault();
        if (!this.touchpadActive) return;

        for (const touch of e.changedTouches) {
            if (touch.identifier === this.touchpadTouchId) {
                this.updateTouchpadPosition(touch.clientX, touch.clientY);
                break;
            }
        }
    }

    onTouchpadEnd(e) {
        e.preventDefault();
        for (const touch of e.changedTouches) {
            if (touch.identifier === this.touchpadTouchId) {
                this.resetTouchpad();
                break;
            }
        }
    }

    onMouseDown(e) {
        this.touchpadActive = true;
        this.touchpadRect = this.touchpad.getBoundingClientRect();
        this.updateTouchpadPosition(e.clientX, e.clientY);
    }

    onMouseMove(e) {
        if (this.touchpadActive) {
            this.updateTouchpadPosition(e.clientX, e.clientY);
        }
    }

    onMouseUp(e) {
        if (this.touchpadActive) {
            this.resetTouchpad();
        }
    }

    updateTouchpadPosition(clientX, clientY) {
        if (!this.touchpadRect) return;

        // Calculate center of touchpad
        const centerX = this.touchpadRect.left + this.touchpadRect.width / 2;
        const centerY = this.touchpadRect.top + this.touchpadRect.height / 2;

        // Calculate offset from center
        let dx = clientX - centerX;
        let dy = clientY - centerY;

        // Normalize to -1 to 1 range based on half the touchpad size
        const maxRadius = Math.min(this.touchpadRect.width, this.touchpadRect.height) / 2;

        // Swap X and Y, invert horizontal: touchpad right -> game up, touchpad down -> game right
        this.moveX = Math.max(-1, Math.min(1, dy / maxRadius));
        this.moveY = Math.max(-1, Math.min(1, -dx / maxRadius));

        // Update indicator position (clamp to circular touchpad bounds)
        const dist = Math.sqrt(dx * dx + dy * dy);
        let indicatorX = dx;
        let indicatorY = dy;
        if (dist > maxRadius) {
            indicatorX = (dx / dist) * maxRadius;
            indicatorY = (dy / dist) * maxRadius;
        }

        this.touchpadIndicator.style.transform = `translate(${indicatorX}px, ${indicatorY}px)`;
        this.touchpadIndicator.classList.add('active');
    }

    resetTouchpad() {
        this.touchpadActive = false;
        this.touchpadTouchId = null;
        this.moveX = 0;
        this.moveY = 0;
        this.touchpadIndicator.style.transform = 'translate(0px, 0px)';
        this.touchpadIndicator.classList.remove('active');
    }

    setupButtons() {
        // Action 1 (with cooldown)
        this.addButtonListeners(this.action1Btn, () => {
            const now = Date.now();
            if (now - this.lastAction1Time < this.buttonCooldown) return;
            this.lastAction1Time = now;
            this.action1 = true;
        }, () => {
            this.action1 = false;
        });

        // Action 2
        this.addButtonListeners(this.action2Btn, () => {
            this.action2 = true;
        }, () => {
            this.action2 = false;
        });
    }

    addButtonListeners(element, onPress, onRelease) {
        if (!element) return;

        // Touch events
        element.addEventListener('touchstart', (e) => {
            e.preventDefault();
            element.classList.add('pressed');
            onPress();
        }, { passive: false });

        element.addEventListener('touchend', (e) => {
            e.preventDefault();
            element.classList.remove('pressed');
            onRelease();
        }, { passive: false });

        element.addEventListener('touchcancel', (e) => {
            e.preventDefault();
            element.classList.remove('pressed');
            onRelease();
        }, { passive: false });

        // Mouse events for desktop testing
        element.addEventListener('mousedown', (e) => {
            e.preventDefault();
            element.classList.add('pressed');
            onPress();
        });

        element.addEventListener('mouseup', (e) => {
            element.classList.remove('pressed');
            onRelease();
        });

        element.addEventListener('mouseleave', (e) => {
            element.classList.remove('pressed');
            onRelease();
        });
    }

    setupStabButton() {
        if (!this.stabBtn) return;

        const onStab = () => {
            this.stabBtn.classList.add('pressed');
            this.stabPressed();
            setTimeout(() => {
                this.stabBtn.classList.remove('pressed');
            }, 150);
        };

        this.stabBtn.addEventListener('touchstart', (e) => {
            e.preventDefault();
            onStab();
        }, { passive: false });

        this.stabBtn.addEventListener('mousedown', (e) => {
            e.preventDefault();
            onStab();
        });
    }

    setupShakeDetection() {
        console.log('Setting up shake detection...');

        if (!window.DeviceMotionEvent) {
            console.log('Device motion not supported');
            return;
        }

        // Request permission for iOS 13+
        if (typeof DeviceMotionEvent.requestPermission === 'function') {
            console.log('iOS detected - will request permission on user interaction');

            const requestPermission = async () => {
                try {
                    const response = await DeviceMotionEvent.requestPermission();
                    console.log('DeviceMotion permission response:', response);
                    if (response === 'granted') {
                        this.addMotionListener();
                    }
                } catch (err) {
                    console.error('DeviceMotion permission error:', err);
                }
            };

            // Listen for both click and touch events
            const handler = () => {
                requestPermission();
                document.removeEventListener('click', handler);
                document.removeEventListener('touchend', handler);
            };

            document.addEventListener('click', handler);
            document.addEventListener('touchend', handler);
        } else {
            // Non-iOS devices
            console.log('Non-iOS device - adding motion listener directly');
            this.addMotionListener();
        }
    }

    setupOrientationTracking() {
        console.log('Setting up orientation tracking...');

        if (!window.DeviceOrientationEvent) {
            console.log('Device orientation not supported');
            return;
        }

        // Request permission for iOS 13+
        if (typeof DeviceOrientationEvent.requestPermission === 'function') {
            console.log('iOS detected - will request orientation permission on user interaction');

            const requestPermission = async () => {
                try {
                    const response = await DeviceOrientationEvent.requestPermission();
                    console.log('DeviceOrientation permission response:', response);
                    if (response === 'granted') {
                        this.addOrientationListener();
                    }
                } catch (err) {
                    console.error('DeviceOrientation permission error:', err);
                }
            };

            const handler = () => {
                requestPermission();
                document.removeEventListener('click', handler);
                document.removeEventListener('touchend', handler);
            };

            document.addEventListener('click', handler);
            document.addEventListener('touchend', handler);
        } else {
            console.log('Non-iOS device - adding orientation listener directly');
            this.addOrientationListener();
        }
    }

    addOrientationListener() {
        console.log('Adding device orientation listener');

        window.addEventListener('deviceorientation', (event) => {
            if (!this.orientationEnabled) return;
            if (event.alpha !== null) {
                let alpha = event.alpha;
                // If phone is upside down (beta outside -90..90), flip 180 degrees
                if (event.beta !== null && (event.beta > 90 || event.beta < -90)) {
                    alpha = (alpha + 180) % 360;
                }
                this.orientAlpha = alpha;
            }
        });
    }

    setStabMode(newStabSpeed) {
        const wasStabMode = this.stabMode;
        this.stabMode = (newStabSpeed > 0);
        this.stabSpeed = newStabSpeed;
        console.log('Stab mode:', this.stabMode ? 'ON' : 'OFF');
        console.log('Stab speed:', newStabSpeed);

        if (!wasStabMode && this.stabMode) {
            this.playBuffer(this.gotchaBuffer);
        }
    }

    addMotionListener() {
        console.log('Adding device motion listener');
        this.motionListenerActive = true;

        window.addEventListener('devicemotion', (event) => {
            this.handleDeviceMotion(event);
        });
    }

    handleDeviceMotion(event) {
        if (!this.motionEnabled) return;

        // Try accelerationIncludingGravity first, fall back to acceleration
        const acceleration = event.accelerationIncludingGravity || event.acceleration;

        if (!this.stabMode || !acceleration || (acceleration.x === null && acceleration.y === null && acceleration.z === null)) {
            return;
        }

        const x = acceleration.x || 0;
        const y = acceleration.y || 0;
        const z = acceleration.z || 0;

        // Only detect Y-axis (up/down motion when phone held upright)
        // This ignores rotation and horizontal movement
        const deltaX = 0;
        const deltaY = Math.abs(y - this.lastAcceleration.y);
        const deltaZ = 0;

        const totalDelta = deltaX + deltaY + deltaZ;

        // Debug logging (enable with: app.controller.debugMotion = true)
        if (this.debugMotion && totalDelta > 1) {
            console.log(`Motion delta: ${totalDelta.toFixed(2)} (threshold: ${this.shakeThreshold })`);
        }

        if (totalDelta > this.shakeThreshold ) {
            const now = Date.now();
            if (now - this.lastShakeTime > this.shakeCooldown / this.stabSpeed) {
                this.lastShakeTime = now;
                console.log(`Shake triggered! Delta: ${totalDelta.toFixed(2)}`);
                this.triggerShake();
            }
        }

        this.lastAcceleration = { x, y, z };
    }

    // Manual trigger for testing (can be called from console: app.controller.testShake())
    testShake() {
        console.log('Manual shake test');
        this.triggerShake();
    }

    stabPressed() {
        if (!this.stabMode) return;
        const now = Date.now();
        if (now - this.lastShakeTime < this.shakeCooldown/ this.stabSpeed) return;
        this.lastShakeTime = now;
        this.triggerShake();
    }

    triggerShake() {
        console.log('Shake detected!');
        this.playShakeSound();

        if (this.onShake) {
            this.onShake();
        }
    }

    async initAudio() {
        try {
            const AudioCtx = window.AudioContext || window.webkitAudioContext;
            this.audioContext = new AudioCtx();
            await this.audioContext.resume();

            const [gotchaResp, stabResp] = await Promise.all([
                fetch('Sounds/gotcha.mp3'),
                fetch('Sounds/Stab.mp3')
            ]);
            const [gotchaData, stabData] = await Promise.all([
                gotchaResp.arrayBuffer(),
                stabResp.arrayBuffer()
            ]);
            [this.gotchaBuffer, this.stabBuffer] = await Promise.all([
                this.audioContext.decodeAudioData(gotchaData),
                this.audioContext.decodeAudioData(stabData)
            ]);
            console.log('Audio buffers decoded');
        } catch (e) {
            console.error('Failed to init audio:', e);
        }
    }

    playBuffer(buffer) {
        try {
            if (!this.audioContext || !buffer) return;
            const source = this.audioContext.createBufferSource();
            source.buffer = buffer;
            source.connect(this.audioContext.destination);
            source.start(0);
        } catch (e) {
            // silently ignore playback errors
        }
    }

    playShakeSound() {
        this.playBuffer(this.stabBuffer);
    }

    startSendingInput() {
        if (this.inputSendInterval) return;

        this.inputSendInterval = setInterval(() => {
            if (this.onInputChange) {
                this.onInputChange(this.moveX, this.moveY, this.action1, this.action2, this.orientAlpha);
            }
        }, this.inputSendRate);
    }

    stopSendingInput() {
        if (this.inputSendInterval) {
            clearInterval(this.inputSendInterval);
            this.inputSendInterval = null;
        }
    }

    reset() {
        this.resetTouchpad();
        this.action1 = false;
        this.action2 = false;
    }

    configureForControllerMode(controllerModeOn) {
        this.controllerMode = controllerModeOn;
        if (controllerModeOn) {
            if (this.stabBtn) this.stabBtn.style.display = '';
            this.motionEnabled = false;
            this.orientationEnabled = false;
        } else {
            if (this.stabBtn) this.stabBtn.style.display = 'none';
            this.motionEnabled = true;
            this.orientationEnabled = true;
        }
        console.log('Controller mode configured:', controllerModeOn ? 'button' : 'shake');
    }

    getInput() {
        return {
            moveX: this.moveX,
            moveY: this.moveY,
            action1: this.action1,
            action2: this.action2
        };
    }
}
