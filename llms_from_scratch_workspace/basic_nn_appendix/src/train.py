import torch
import torch.nn.functional as F

from basic_nn_appendix.src.types.network import NeuralNetwork



def train(model, loader, num_epochs, device):
    # torch.manual_seed(123)
    optimizer = torch.optim.SGD(model.parameters(), lr=0.5)

    for epoch in range(num_epochs):
        model.train()
        for batch_idx, (features, labels) in enumerate(loader):
            features = features.to(device)
            labels = labels.to(device)
            logits = model(features)

            loss = F.cross_entropy(logits, labels)

            optimizer.zero_grad()
            loss.backward()
            optimizer.step()

            print(f"Epoch: {epoch+1:03d}/{num_epochs:03d} | Batch {batch_idx+1:03d}/{len(loader):03d} | Train Loss: {loss:.2f}")

        model.eval()