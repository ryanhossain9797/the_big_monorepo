import torch
from torch.utils.data import DataLoader

from basic_nn_appendix.src.train import train
from basic_nn_appendix.src.datasets.dataset import train_dataset, test_dataset
from basic_nn_appendix.src.types.network import NeuralNetwork

device = "cuda" if torch.cuda.is_available() else "cpu"

print(device)

train_loader = DataLoader(train_dataset, batch_size=2, shuffle=True, num_workers=0, drop_last=True)
test_loader = DataLoader(test_dataset, batch_size=2, shuffle=False, num_workers=0, drop_last=True)


model = NeuralNetwork(input_size=2, output_size=2).to(device)

train(model, train_loader, num_epochs=5, device=device)

model.eval()
with torch.no_grad():
    outputs = model(train_dataset.features.to(device))

print(outputs)

torch.set_printoptions(sci_mode=False)
probas = torch.softmax(outputs, dim=1)
predictions = torch.argmax(outputs, dim=1)
print(predictions)

