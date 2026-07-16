% animate_to_unity.m
% Loads the MVNX motion capture file and streams it to Unity via UDP.
% This is our working version for Mac (no Instrument Control Toolbox needed).
% Anna's original script is preserved in load_mvnx_and_animate.m

% Load data
filename = 'session_tig100126-003';
tree = load_mvnx(filename);
frameRate = tree.subject.frameRate;
size_vec_debug = length(tree.subject.frames.frame) - 3;
quats2debug = zeros(4, 23, size_vec_debug);
pos2debug   = zeros(3, 23, size_vec_debug);
for j = 4:length(tree.subject.frames.frame)
    all_quats = tree.subject.frames.frame(j).orientation;
    quats2debug(:,:,j) = reshape(all_quats, 4, 23);
    all_pos = tree.subject.frames.frame(j).position;
    pos2debug(:,:,j)   = reshape(all_pos,   3, 23);
end

% Open UDP socket using Java (no toolbox required)
socket  = java.net.DatagramSocket();
% Destination IP. Default = the Unity Editor on this machine.
% To stream to a headset, comment the line below, uncomment the next one, and put
% YOUR device's IP there (per-network — the example is not a fixed address).
% NOTE: on-device pose needs XsensUDPReceiver in the scene. The Live Capture plugin
% is Editor-only and never binds 9763 in a build — see TASKS.md.
address = java.net.InetAddress.getByName('127.0.0.1');    %if running inside the editor
%address = java.net.InetAddress.getByName('192.168.68.100'); %on headset — YOUR device's IP


start_time      = cputime;
message_counter = 1;
ind_deb         = 1;

f = figure('Name', 'Press a key to stop the loop', 'Position', [100, 100, 400, 200]);
set(f, 'CurrentCharacter', ' ');
disp('Loop running. Click the figure window and press any key (not space) to stop.');

while true

    % Build MXTP02 packet (XSens MVN protocol, 23 body segments)
    A1 = zeros(760, 1, 'uint8');
    A1(1:6)   = [77;88;84;80;48;50];                                   % "MXTP02"
    A1(7:10)  = fliplr(typecast(uint32(message_counter), 'uint8'))';   % sample counter
    message_counter = message_counter + 1;
    A1(11)    = 128;
    A1(12)    = 23;
    time      = round((cputime - start_time) * 1e3);
    A1(13:16) = fliplr(typecast(uint32(time), 'uint8'))';              % timestamp
    A1(17)    = 0;   % actor ID (Unity Character ID = 1 maps to protocol ID = 0)
    A1(18)    = 23;
    A1(23:24) = [2; 224];                                              % payload size

    quats_Unity = quats2debug(:,:,ind_deb);
    pos_Unity   = pos2debug(:,:,ind_deb);
    if ind_deb < size(quats2debug, 3)
        ind_deb = ind_deb + 1;
    else
        ind_deb = 1;
    end

    k = 25;
    for i = 1:23
        A1(k:k+3) = fliplr(typecast(uint32(i),               'uint8'))'; k = k+4;
        A1(k:k+3) = fliplr(typecast(single(pos_Unity(1,i)),  'uint8'))'; k = k+4;
        A1(k:k+3) = fliplr(typecast(single(pos_Unity(2,i)),  'uint8'))'; k = k+4;
        A1(k:k+3) = fliplr(typecast(single(pos_Unity(3,i)),  'uint8'))'; k = k+4;
        A1(k:k+3) = fliplr(typecast(single(quats_Unity(1,i)),'uint8'))'; k = k+4;
        A1(k:k+3) = fliplr(typecast(single(quats_Unity(2,i)),'uint8'))'; k = k+4;
        A1(k:k+3) = fliplr(typecast(single(quats_Unity(3,i)),'uint8'))'; k = k+4;
        A1(k:k+3) = fliplr(typecast(single(quats_Unity(4,i)),'uint8'))'; k = k+4;
    end

    % Send packet
    javaData = typecast(A1, 'int8');
    packet   = java.net.DatagramPacket(javaData, int32(length(javaData)), address, int32(9763));
    socket.send(packet);

    pause(1/frameRate);  % play back at the original recorded speed
    drawnow;

    if ~ishandle(f) || ~strcmp(get(f, 'CurrentCharacter'), ' ')
        disp('Key pressed! Exiting loop.');
        break;
    end

end

socket.close();
disp('Loop terminated. Closing figure.');
close(f);
