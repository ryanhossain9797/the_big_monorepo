sudo ~/apps/maelstrom/maelstrom test -w unique-ids --bin ./target/release/dist_sys --time-limit 30 --rate 1000 --node-count 3 --availability total --nemesis partition

sudo ~/apps/maelstrom/maelstrom test -w broadcast --bin ./target/release/dist_sys --node-count 5 --time-limit 20 --rate 10