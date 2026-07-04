% sim_to_unity.m
% Synthetic test sender for the lab physics stream -> Unity (SimulatorReceiver.cs).
% Flies a banked spiral descent so you can VERIFY the whole pipeline end to end
% WITHOUT any recorded data file: the avatar should descend and circle, the canopy
% should bank and turn its heading, and the arms should pull down alternately as the
% steering toggles oscillate.
%
% Pure-Java UDP (no Instrument Control Toolbox needed). Run AFTER pressing Play in Unity.
%
% --- WIRE FORMAT ----------------------------------------------------------
% One ASCII line of 26 comma-separated numbers per frame, in the EXACT field order
% of the simulator's `x_s` struct that SimulatorReceiver.cs expects
% (b = canopy/parachute, s = skydiver/store):
%    1  t
%    2..4   x_i_b y_i_b z_i_b     canopy position   (unused by the renderer)
%    5..7   x_i_s y_i_s z_i_s     skydiver position (m)
%    8..10  Vx_b Vy_b Vz_b        canopy velocity   (m/s)   -- HUD SPD/HDG
%    11..12 Vx_s Vy_s             skydiver velocity (no Vz_s in the sim)
%    13..15 p_b q_b r_b           canopy body rates (rad/s)
%    16..18 p_s q_s r_s           skydiver body rates
%    19..21 phi_b theta_b psi_b   canopy attitude   roll,pitch,yaw (deg)
%    22..24 phi_s theta_s psi_s   skydiver attitude
%    25     delta_l               left toggle  (normalized; full pull ~0.01)
%    26     delta_r               right toggle
% Frame = simulator NED (X north/forward, Y east/right, Z down), 3-2-1 Euler degrees.
% Position Z is sent as ALTITUDE (matches the example data + positionZIsAltitude=ON).

host = '127.0.0.1';
port = 9764;          % must match SimulatorReceiver.listenPort
rate = 50;            % Hz
T    = 60;            % seconds to fly

% --- flight parameters ----------------------------------------------------
R        = 40;        % spiral radius (m)
z0       = 200;       % start altitude (m)
descend  = 5;         % descent rate (m/s, down-positive)
turnRate = 25;        % heading change (deg/s) -> also drives the circle
bankDeg  = 15;        % canopy bank angle into the turn (visual)
canopyUp = 2;         % canopy sits ~2 m above the skydiver CG (sim convention)

socket  = java.net.DatagramSocket();
address = java.net.InetAddress.getByName(host);
fprintf('Sending 26-field spiral-descent test to %s:%d at %d Hz for %ds...\n', host, port, rate, T);
cleanup = onCleanup(@() socket.close());

t0 = tic;
n  = 0;
while toc(t0) < T
    t = toc(t0);

    % Circular path in NED (X north, Y east). Altitude counts down from z0.
    ang   = deg2rad(turnRate * t);
    north = R * cos(ang);
    east  = R * sin(ang);
    alt   = max(z0 - descend * t, bankDeg*0 + 5);   % stop ~5 m up

    x_i_b = [north, east, alt + canopyUp];          % canopy pos (ignored by renderer)
    x_i_s = [north, east, alt];                     % skydiver CG pos (drives the avatar)

    % Velocities -- analytic derivatives of the circle; Vz is down-positive.
    w  = deg2rad(turnRate);
    vN = -R * w * sin(ang);
    vE =  R * w * cos(ang);
    V_b = [vN, vE, descend];                        % canopy Vx,Vy,Vz  (HUD)
    V_s = [vN, vE];                                 % skydiver Vx,Vy    (no Vz_s)

    % Body rates (rad/s): only yaw about the down axis is non-zero.
    rate_b = [0, 0, w];                             % p_b q_b r_b
    rate_s = [0, 0, w];                             % p_s q_s r_s

    % Attitude (deg), NED 3-2-1. Canopy banks into the turn; heading = yaw.
    yaw     = mod(turnRate * t, 360);
    att_b   = [bankDeg, 0, yaw];                    % phi,theta,psi  (roll banks)
    att_s   = [0,       0, yaw];                    % skydiver kept upright

    % Steering toggles, normalized (full pull ~0.01). Oscillate out of phase so the
    % arms pull down alternately (toggleScale=100 in Unity maps 0.01 -> 1.0).
    delta_l = 0.005 + 0.005 * sin(2*pi*0.2*t);
    delta_r = 0.005 + 0.005 * sin(2*pi*0.2*t + pi);

    vals = [t, x_i_b, x_i_s, V_b, V_s, rate_b, rate_s, att_b, att_s, delta_l, delta_r];
    % Sanity: this must be exactly 26 numbers.
    % assert(numel(vals) == 26)

    msg    = strjoin(arrayfun(@(v) sprintf('%.5f', v), vals, 'UniformOutput', false), ',');
    bytes  = int8(uint8(msg));
    packet = java.net.DatagramPacket(bytes, numel(bytes), address, int32(port));
    socket.send(packet);

    n = n + 1;
    pause(1/rate);
end

fprintf('Done. Sent %d packets.\n', n);
