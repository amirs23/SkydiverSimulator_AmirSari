% sim_replay.m
% Replays a recorded simulator run (the example sim_out.mat) to Unity over UDP,
% so you can test SimulatorReceiver.cs against REAL simulator data with no lab
% hardware. Streams the exact 26 fields of the `x_s` struct, in struct order.
%
% Pure-Java UDP (no Instrument Control Toolbox needed) -- same approach as
% animate_to_unity.m. Run this AFTER pressing Play in Unity.
%
% WIRE FORMAT: one ASCII line of 26 comma-separated numbers per frame, matching
% SimulatorReceiver.cs (b = canopy, s = skydiver):
%   t,
%   x_i_b,y_i_b,z_i_b, x_i_s,y_i_s,z_i_s,
%   Vx_b,Vy_b,Vz_b, Vx_s,Vy_s,
%   p_b,q_b,r_b, p_s,q_s,r_s,
%   phi_b,theta_b,psi_b, phi_s,theta_s,psi_s,
%   delta_l, delta_r
% Frame = simulator NED (X fwd, Y right, Z down per the spec; in this example the
% position Z reads as altitude). Angles in degrees, rates in rad/s.

% --- config ---------------------------------------------------------------
% Look for sim_out.mat right next to this script (i.e. in the repo's Matlab
% folder) so it works on any machine once the file is dropped in. Ask Sari to
% share sim_out.mat and place it in this folder. To test WITHOUT the recording,
% run sim_to_unity instead (synthetic flight, no data file needed).
thisDir  = fileparts(mfilename('fullpath'));
matFile  = fullfile(thisDir, 'sim_out.mat');
if ~isfile(matFile)
    error(['sim_replay: sim_out.mat not found in\n  %s\n' ...
           'This is the lab simulator recording (not committed to GitHub -- ask Sari for it),\n' ...
           'drop it into the Matlab folder, then run sim_replay again.\n' ...
           'To test the pipeline WITHOUT it, run sim_to_unity instead.'], thisDir);
end

% Destination IP. Default = the Unity Editor on this machine.
% To stream to a headset, comment the line below, uncomment the next one, and put
% YOUR device's IP there (per-network — the example is not a fixed address).
host     = '127.0.0.1';        %if running in the Editor on this pc
%host    = '192.168.68.100';   %if running on the device — replace with YOUR headset's IP
port     = 9764;          % must match SimulatorReceiver.listenPort
sendRate = 50;            % Hz to send at (sim is logged at 100 Hz)
speed    = 1.0;           % playback speed multiplier (2.0 = twice as fast)
loop     = true;          % restart from the top when the run ends

% --- load the recording ---------------------------------------------------
S = load(matFile);
fn = fieldnames(S);
x  = S.(fn{1});           % the struct (named x_s in the example)

% Pull every channel as a flat column. Vz_s is absent in the example data.
col = @(name) double(x.(name)(:));
t      = col('t');
x_i_b  = col('x_i_b');  y_i_b = col('y_i_b');  z_i_b = col('z_i_b');
x_i_s  = col('x_i_s');  y_i_s = col('y_i_s');  z_i_s = col('z_i_s');
Vx_b   = col('V_x_i_b'); Vy_b = col('V_y_i_b'); Vz_b = col('V_z_i_b');
Vx_s   = col('V_x_i_s'); Vy_s = col('V_y_i_s');
p_b = col('p_b'); q_b = col('q_b'); r_b = col('r_b');
p_s = col('p_s'); q_s = col('q_s'); r_s = col('r_s');
phi_b = col('phi_b'); theta_b = col('theta_b'); psi_b = col('psi_b');
phi_s = col('phi_s'); theta_s = col('theta_s'); psi_s = col('psi_s');
delta_l = col('delta_l'); delta_r = col('delta_r');

nSamples = numel(t);

% Step through the log so the wall-clock playback matches `sendRate`/`speed`,
% regardless of the log's own 100 Hz spacing.
dtLog  = median(diff(t));                 % ~0.01 s
stride = max(1, round((1/sendRate) * speed / dtLog));

% --- UDP socket -----------------------------------------------------------
socket  = java.net.DatagramSocket();
address = java.net.InetAddress.getByName(host);
fprintf('Replaying %s (%d samples) to %s:%d at ~%d Hz (speed x%.1f)...\n', ...
        matFile, nSamples, host, port, sendRate, speed);

cleanup = onCleanup(@() socket.close());

while true
    tic0 = tic;
    sent = 0;
    for i = 1:stride:nSamples
        vals = [ t(i), ...
                 x_i_b(i), y_i_b(i), z_i_b(i), ...
                 x_i_s(i), y_i_s(i), z_i_s(i), ...
                 Vx_b(i), Vy_b(i), Vz_b(i), ...
                 Vx_s(i), Vy_s(i), ...
                 p_b(i), q_b(i), r_b(i), ...
                 p_s(i), q_s(i), r_s(i), ...
                 phi_b(i), theta_b(i), psi_b(i), ...
                 phi_s(i), theta_s(i), psi_s(i), ...
                 delta_l(i), delta_r(i) ];

        msg    = strjoin(arrayfun(@(v) sprintf('%.6f', v), vals, 'UniformOutput', false), ',');
        bytes  = int8(uint8(msg));
        packet = java.net.DatagramPacket(bytes, numel(bytes), address, int32(port));
        socket.send(packet);

        sent = sent + 1;
        pause(1/sendRate);
    end
    fprintf('Sent %d packets (%.1fs of sim time).\n', sent, toc(tic0));
    if ~loop, break; end
end
