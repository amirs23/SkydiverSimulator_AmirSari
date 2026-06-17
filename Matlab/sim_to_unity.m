% sim_to_unity.m
% Test sender for the lab physics stream -> Unity (SimulatorReceiver.cs).
% Flies a synthetic spiral descent so you can VERIFY the pipeline end to end:
% the canopy should circle, descend, turn its heading, and the avatar's arms
% should pull down as the steering inputs oscillate.
%
% Pure-Java UDP (no Instrument Control Toolbox needed) — same approach as
% animate_to_unity.m. Run this AFTER pressing Play in Unity.
%
% WIRE FORMAT: one ASCII line of 29 comma-separated numbers per frame
% (see SimulatorReceiver.cs for the field order). Frame = ENU, Z up (altitude),
% X east, Y north, meters; angles in degrees; steering 0..1.

host = '127.0.0.1';
port = 9764;          % must match SimulatorReceiver.listenPort
rate = 50;            % Hz
T    = 60;            % seconds to fly

% --- flight parameters ----------------------------------------------------
R        = 40;        % spiral radius (m)
z0       = 200;       % start altitude (m)
descend  = 5;         % descent rate (m/s)
turnRate = 25;        % heading change (deg/s)  -> also drives the circle
bodyDrop = 5;         % skydiver hangs 5 m below the canopy

socket  = java.net.DatagramSocket();
address = java.net.InetAddress.getByName(host);
fprintf('Sending spiral-descent test to %s:%d at %d Hz for %ds...\n', host, port, rate, T);

t0 = tic;
n  = 0;
while toc(t0) < T
    t = toc(t0);

    % Heading and circular path (ENU: X east, Y north, Z up)
    yaw   = mod(turnRate * t, 360);              % canopy heading (deg)
    ang   = deg2rad(turnRate * t);
    cx    = R * cos(ang);
    cy    = R * sin(ang);
    cz    = max(z0 - descend * t, bodyDrop);     % altitude, stop at body height

    canopyPos = [cx, cy, cz];
    bodyPos   = [cx, cy, max(cz - bodyDrop, 0)]; % CG below the canopy

    % Orientations (deg): gentle bank into the turn; body roughly upright
    canopyRPY = [15, 0, yaw];                    % roll, pitch, yaw
    bodyRPY   = [0,  0, yaw];

    % Velocities (rough analytic derivatives)
    w  = deg2rad(turnRate);
    vx = -R * w * sin(ang);
    vy =  R * w * cos(ang);
    vz = -descend;
    canopyVel = [vx, vy, vz];
    bodyVel   = [vx, vy, vz];

    canopyAngVel = [0, 0, turnRate];             % deg/s about up
    bodyAngVel   = [0, 0, turnRate];

    wind = [3, 0, 0];                            % steady 3 m/s easterly

    % Steering: oscillate so you can see the arms pull down and the canopy react.
    rightSteer = 0.5 + 0.5 * sin(2*pi*0.2*t);    % 0..1
    leftSteer  = 0.5 + 0.5 * sin(2*pi*0.2*t + pi);

    vals = [canopyPos, bodyPos, canopyRPY, bodyRPY, ...
            canopyVel, bodyVel, canopyAngVel, bodyAngVel, ...
            wind, rightSteer, leftSteer];

    msg   = strjoin(arrayfun(@(x) sprintf('%.5f', x), vals, 'UniformOutput', false), ',');
    bytes = int8(uint8(msg));
    packet = java.net.DatagramPacket(bytes, numel(bytes), address, int32(port));
    socket.send(packet);

    n = n + 1;
    pause(1/rate);
end

socket.close();
fprintf('Done. Sent %d packets.\n', n);
