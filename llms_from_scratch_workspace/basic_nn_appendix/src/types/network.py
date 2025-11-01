import torch

class NeuralNetwork(torch.nn.Module):
    def __init__(self, input_size, output_size):
        super().__init__()
        self.layers = torch.nn.Sequential(
            torch.nn.Linear(input_size, 30),
            torch.nn.ReLU(),
            torch.nn.Linear(30, 20),
            torch.nn.ReLU(),
            torch.nn.Linear(20, output_size)
        )

    def forward(self, x):
        logits = self.layers(x)
        return logits

# torch.manual_seed(123)
# model = NeuralNetwork(50, 3)
# print(model.layers[0].weight)

# torch.manual_seed(123)
# X = torch.rand((1, 50))
# with torch.no_grad():
#     out = torch.softmax(model(X), dim=1)
# print(out)